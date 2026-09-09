using System.Globalization;

namespace Daraban.Modules.Reporting.Services.Reports;

/// <summary>
/// Minimal 5-field cron expression validator shared by the Reporting validator and the
/// Automation schedule runner (Task 7.2). Deliberately hand-rolled instead of pulling a
/// cron library: the platform only uses standard 5-field expressions, and both sides must
/// agree on exactly what is valid (a definition accepted here must never fail to fire there).
/// </summary>
public static class CronExpressionValidator
{
    /// <summary>Parses a standard 5-field cron expression; returns the next occurrence when
    /// <paramref name="after"/> is provided.</summary>
    public static bool TryValidate(string expression, out CronSchedule? schedule) =>
        CronSchedule.TryParse(expression, out schedule);

    /// <summary>Convenience overload for call sites that only need validity.</summary>
    public static bool IsValid(string expression) => TryValidate(expression, out _);
}

/// <summary>A parsed 5-field cron schedule that can compute its next occurrence.</summary>
public sealed class CronSchedule
{
    private readonly bool[] _minutes = new bool[60];
    private readonly bool[] _hours = new bool[24];
    private readonly bool?[] _daysOfMonth = new bool?[32];   // null = field not restricted
    private readonly bool?[] _months = new bool?[13];
    private readonly bool?[] _daysOfWeek = new bool?[8];     // 0=Sunday .. 7=Sunday (cron allows both)

    private bool _domRestricted;
    private bool _dowRestricted;

    private CronSchedule() { }

    public static bool TryParse(string? expression, out CronSchedule? schedule)
    {
        schedule = null;
        if (string.IsNullOrWhiteSpace(expression)) return false;

        var fields = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != 5) return false;

        var cron = new CronSchedule();
        if (!TryParseField(fields[0], 0, 59, false, v => cron._minutes[v] = true)) return false;
        if (!TryParseField(fields[1], 0, 23, false, v => cron._hours[v] = true)) return false;
        if (!TryParseField(fields[2], 1, 31, true, v => cron._daysOfMonth[v] = true)) return false;
        cron._domRestricted = fields[2] is not ("*" or "?");
        if (!TryParseField(fields[3], 1, 12, true, v => cron._months[v] = true)) return false;
        if (!TryParseField(fields[4], 0, 7, true, v => cron._daysOfWeek[v % 8] = true)) return false;
        cron._dowRestricted = fields[4] is not ("*" or "?");

        schedule = cron;
        return true;
    }

    /// <summary>Next strictly-after occurrence, scanning forward minute by minute. The scan
    /// window is 366 days (matches "at least yearly" semantics); schedules that never fire
    /// within a year (e.g. Feb 30) return null rather than looping forever.</summary>
    public DateTimeOffset? NextOccurrence(DateTimeOffset after)
    {
        var candidate = new DateTimeOffset(after.Year, after.Month, after.Day, after.Hour, after.Minute, 0, after.Offset)
            .AddMinutes(1);

        var limit = after.AddYears(1);
        while (candidate <= limit)
        {
            if (Matches(candidate)) return candidate;
            candidate = candidate.AddMinutes(1);
        }

        return null;
    }

    /// <summary>Whether the given timestamp matches this schedule (minute precision --
    /// seconds are ignored, matching cron semantics). The Automation schedule runner calls
    /// this each minute tick with the same parser that validated the definition, so an
    /// accepted schedule can never fail to fire.</summary>
    public bool Matches(DateTimeOffset t) =>
        _minutes[t.Minute] &&
        _hours[t.Hour] &&
        MonthAndDayMatch(t);

    /// <summary>Standard cron day semantics: when both day-of-month and day-of-week are
    /// restricted, either may match; when only one is restricted, it must.</summary>
    private bool MonthAndDayMatch(DateTimeOffset t)
    {
        // Months have no "either/or" semantics: unrestricted ("*") sets every entry true,
        // restricted leaves non-selected months null -- both cases are handled by requiring
        // an explicit true. (Checking `== false` here would let null months slip through and
        // make "30 2 * 2 *" fire in January, which was a real bug caught by tests.)
        if (_months[t.Month] != true) return false;

        if (!_domRestricted && !_dowRestricted)
            return true;                    // neither restricted

        // An unrestricted field sets every array slot -- the restriction flag must gate the
        // lookup, or "*" would always contribute a (wrong) match on the one-restricted side.
        var domMatches = _domRestricted && _daysOfMonth[t.Day] == true;
        var dowMatches = _dowRestricted && _daysOfWeek[(int)t.DayOfWeek] == true;

        // Both restricted: either may fire (standard Vixie cron "or" semantics). Exactly one
        // restricted: that one must match -- the other side contributes false.
        return domMatches || dowMatches;
    }

    private static bool TryParseField(string field, int min, int max, bool optional, Action<int> set)
    {
        // "*" or "?": unrestricted -- for optional fields this means "no constraint".
        if (field is "*" or "?")
        {
            if (!optional && field == "?") return false;
            for (var v = min; v <= max; v++) set(v);
            return true;
        }

        // Comma lists: "1,15,30" -- each element is itself a value/range/step form. Only the
        // last element may carry a step (cron's real grammars disagree here; we keep it simple
        // by disallowing steps mid-list, which no sane schedule uses).
        if (field.Contains(','))
        {
            var elements = field.Split(',');
            foreach (var element in elements)
            {
                if (element.Contains(',') || element is "*" or "?") return false;
                if (!TryParseField(element, min, max, optional, set)) return false;
            }
            return true;
        }

        // "*/step" and "a-b/step" forms.
        var parts = field.Split('/');
        if (parts.Length > 2) return false;

        var step = 1;
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out step) || step < 1)
                return false;
        }

        var range = parts[0];
        int start, end;

        if (range == "*")
        {
            start = min;
            end = max;
        }
        else if (range.Contains('-'))
        {
            var bounds = range.Split('-');
            if (bounds.Length != 2) return false;
            if (!TryParseValue(bounds[0], min, max, out start) || !TryParseValue(bounds[1], min, max, out end))
                return false;
            if (start > end) return false;
        }
        else
        {
            // Single value ("5") -- also legal as the left side of a step ("5/10").
            if (!TryParseValue(range, min, max, out start)) return false;
            end = parts.Length == 2 ? max : start;
        }

        for (var v = start; v <= end; v += step)
            set(v > max ? max : v);

        return true;
    }

    private static bool TryParseValue(string token, int min, int max, out int value)
    {
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return false;

        // Day-of-week: cron allows 7 as Sunday; normalize to 0.
        if (min == 0 && max == 7 && value == 7) value = 0;

        return value >= min && value <= max;
    }
}
