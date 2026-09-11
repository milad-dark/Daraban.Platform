using System.Net.Mail;
using Daraban.Modules.Settings.Data;

namespace Daraban.Modules.Settings.Services;

/// <summary>
/// Shared per-key validation rules for PUT /api/v1/settings/{key} (Task 7.4). Static
/// methods rather than a FluentValidation AbstractValidator because the rules are keyed
/// off the catalog definition, not a single request shape. Security-relevant encoding
/// rules (no control characters, printable ASCII only) live here so every path validates
/// identically.
/// </summary>
public static partial class SettingValueValidator
{
    /// <summary>Generic cap applied to every value before per-key checks.</summary>
    public const int MaxValueLength = 500;

    private static readonly string[] AllowedTlsModes = ["none", "starttls", "ssl"];

    /// <summary>Validates the new value against the catalog definition. Returns an error
    /// code and message when invalid; null when the value is acceptable.</summary>
    public static (string Code, string Message)? Validate(string key, string value)
    {
        var definition = SettingCatalog.Find(key);
        if (definition is null)
        {
            return ("SETTINGS.KEY_UNKNOWN", $"Unknown setting '{key}'.");
        }

        if (value.Length > MaxValueLength)
        {
            return ("SETTINGS.VALUE_TOO_LONG", $"Setting values are limited to {MaxValueLength} characters.");
        }

        // Header-injection / control-character hardening: values end up in SMTP envelopes,
        // directory filters, emails and HTML -- none of which tolerate raw CR/LF. Printable
        // ASCII only keeps every downstream consumer safe.
        if (value.Any(c => c is < (char)32 or > (char)126))
        {
            return ("SETTINGS.INVALID_CHARACTERS", "Setting values may only contain printable ASCII characters.");
        }

        if (value != value.Trim())
        {
            return ("SETTINGS.INVALID_CHARACTERS", "Setting values must not start or end with whitespace.");
        }

        return definition.Type switch
        {
            SettingValueType.Int => int.TryParse(value, out _)
                ? null
                : ("SETTINGS.INVALID_INT", "Value must be a whole number."),
            SettingValueType.Boolean => value.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("false", StringComparison.OrdinalIgnoreCase)
                ? null
                : ("SETTINGS.INVALID_BOOLEAN", "Value must be true or false."),
            SettingValueType.Time => ValidateTime(value),
            _ => ValidateString(key, value),
        };
    }

    /// <summary>Strict HH:mm (24h): the display format is a presentation concern, the
    /// storage format must stay unambiguous.</summary>
    private static (string, string)? ValidateTime(string value)
    {
        if (value.Length != 5 || value[2] != ':' ||
            !char.IsAsciiDigit(value[0]) || !char.IsAsciiDigit(value[1]) ||
            !char.IsAsciiDigit(value[3]) || !char.IsAsciiDigit(value[4]))
        {
            return ("SETTINGS.INVALID_TIME", "Value must be a time of day in HH:mm (24-hour) format.");
        }

        var hour = (value[0] - '0') * 10 + (value[1] - '0');
        var minute = (value[3] - '0') * 10 + (value[4] - '0');
        return hour > 23 || minute > 59
            ? ("SETTINGS.INVALID_TIME", "Value must be a time of day in HH:mm (24-hour) format.")
            : null;
    }

    private static (string Code, string Message)? ValidateString(string key, string value)
    {
        return key switch
        {
            "email.smtp_tls" => AllowedTlsModes.Contains(value, StringComparer.OrdinalIgnoreCase)
                ? null
                : ("SETTINGS.INVALID_TLS_MODE", "TLS mode must be one of: none, starttls, ssl."),
            "email.from_address" => MailAddress.TryCreate(value, out _)
                ? null
                : ("SETTINGS.INVALID_EMAIL", "Value must be a valid email address."),
            "branding.logo_url" => Uri.TryCreate(value, UriKind.Absolute, out var uri)
                    && uri.Scheme is "https" or "http"
                ? null
                : ("SETTINGS.INVALID_URL", "Value must be an absolute http(s) URL."),
            "branding.primary_color" => Regexes.HexColor().IsMatch(value)
                ? null
                : ("SETTINGS.INVALID_COLOR", "Value must be a hex color like #1976d2."),
            "time.default_timezone" => TimeZoneInfo.TryFindSystemTimeZoneById(value, out _)
                ? null
                : ("SETTINGS.INVALID_TIMEZONE", "Value must be a valid IANA timezone (e.g. UTC, Europe/Berlin)."),
            "time.date_format" => IsPlausibleDateFormat(value)
                ? null
                : ("SETTINGS.INVALID_DATE_FORMAT", "Provide a reasonable .NET date format (e.g. yyyy-MM-dd)."),
            "ldap.server" => value.Length > 0
                ? null
                : ("SETTINGS.REQUIRED", "Value must not be empty."),
            _ => null,
        };
    }

    /// <summary>Crude but effective sanity cap on the date format: keeps separators from
    /// dominating (e.g. "---") and bounds the length. Full format validation is not worth
    /// the complexity; worst case the UI shows a strangely formatted date.</summary>
    private static bool IsPlausibleDateFormat(string value) =>
        value.Length is >= 3 and <= 40 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '.' or ' ') &&
        !value.Contains("---");

    /// <summary>Compiled-regex singletons: compilation is expensive and these run on every update.</summary>
    private static partial class Regexes
    {
        [System.Text.RegularExpressions.GeneratedRegex(@"^#[0-9a-fA-F]{6}$")]
        public static partial System.Text.RegularExpressions.Regex HexColor();
    }
}
