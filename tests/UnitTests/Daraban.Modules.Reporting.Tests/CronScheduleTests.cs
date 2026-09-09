using Daraban.Modules.Reporting.Services.Reports;
using Xunit;

namespace Daraban.Modules.Reporting.Tests;

/// <summary>
/// The cron parser is the risky part of scheduled reporting (Task 7.2): the definition
/// validator and the Automation-minute-runner must agree on exactly what is valid, and a
/// wrong "matches" answer silently skips or double-fires a report. These tests pin the
/// semantics both call sites depend on. No database is contacted.
/// </summary>
public class CronScheduleTests
{
    private static DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("* * * * *", true)]
    [InlineData("0 0 * * *", true)]
    [InlineData("*/15 * * * *", true)]
    [InlineData("30 2 1,15 * 1-5", true)]
    [InlineData("0 9 ? * MON", false)]        // names not supported (documented limitation)
    [InlineData("61 * * * *", false)]         // minute out of range
    [InlineData("* 25 * * *", false)]         // hour out of range
    [InlineData("0 0 * *", false)]            // too few fields
    [InlineData("0 0 * * * *", false)]        // too many fields
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void TryParse_AcceptsExactlyStandardFiveFieldExpressions(string expression, bool expected)
    {
        Assert.Equal(expected, CronExpressionValidator.TryValidate(expression, out _));
    }

    [Fact]
    public void EveryMinute_MatchesEveryMinute()
    {
        CronSchedule.TryParse("* * * * *", out var schedule);
        Assert.True(schedule!.Matches(At(2026, 9, 9, 14, 37)));
    }

    [Fact]
    public void DailyAt0300_MatchesOnlyThatMinute()
    {
        CronSchedule.TryParse("0 3 * * *", out var schedule);

        Assert.True(schedule!.Matches(At(2026, 9, 9, 3, 0)));
        Assert.False(schedule.Matches(At(2026, 9, 9, 3, 1)));
        Assert.False(schedule.Matches(At(2026, 9, 9, 15, 0)));
    }

    [Fact]
    public void StepMinute_FiresOnMultiples()
    {
        CronSchedule.TryParse("*/15 * * * *", out var schedule);

        Assert.True(schedule!.Matches(At(2026, 9, 9, 5, 0)));
        Assert.True(schedule.Matches(At(2026, 9, 9, 5, 45)));
        Assert.False(schedule.Matches(At(2026, 9, 9, 5, 20)));
    }

    [Fact]
    public void RangeList_MatchesListedValues()
    {
        CronSchedule.TryParse("30 2 1,15 * *", out var schedule);

        Assert.True(schedule!.Matches(At(2026, 9, 1, 2, 30)));
        Assert.True(schedule.Matches(At(2026, 9, 15, 2, 30)));
        Assert.False(schedule.Matches(At(2026, 9, 10, 2, 30)));
    }

    [Fact]
    public void DayOfWeek_ZeroAndSevenBothMeanSunday()
    {
        CronSchedule.TryParse("0 12 * * 0", out var sundaySchedule);
        CronSchedule.TryParse("0 12 * * 7", out var sevenSchedule);

        // 2026-09-06 is a Sunday.
        Assert.True(sundaySchedule!.Matches(At(2026, 9, 6, 12, 0)));
        Assert.True(sevenSchedule!.Matches(At(2026, 9, 6, 12, 0)));
        Assert.False(sundaySchedule.Matches(At(2026, 9, 7, 12, 0)));
    }

    [Fact]
    public void WeekdayRange_MatchesOnlyWeekdays()
    {
        CronSchedule.TryParse("0 8 * * 1-5", out var schedule);

        // 2026-09-07 is a Monday, 2026-09-06 a Sunday.
        Assert.True(schedule!.Matches(At(2026, 9, 7, 8, 0)));
        Assert.False(schedule.Matches(At(2026, 9, 6, 8, 0)));
    }

    [Fact]
    public void DayOfMonthAndDayOfWeek_BothRestrictedMeansEitherCanFire()
    {
        // Vixie cron semantics: when both day fields are restricted, a day matching EITHER
        // fires. 2026-09-15 is a Tuesday (dow 2) and the 15th (dom 15) -- matches both ways.
        CronSchedule.TryParse("0 4 15 * 2", out var schedule);

        Assert.True(schedule!.Matches(At(2026, 9, 15, 4, 0)));
        // 2026-09-22 is a Tuesday but not the 15th -> fires via dow.
        Assert.True(schedule.Matches(At(2026, 9, 22, 4, 0)));
        // 2026-09-10 is a Thursday and the 10th -> neither matches.
        Assert.False(schedule.Matches(At(2026, 9, 10, 4, 0)));
    }

    [Fact]
    public void NextOccurrence_SkipsForwardToTheNextMatch()
    {
        CronSchedule.TryParse("30 2 * * *", out var schedule);

        var next = schedule!.NextOccurrence(At(2026, 9, 9, 10, 0));
        Assert.Equal(At(2026, 9, 10, 2, 30), next);
    }

    [Fact]
    public void NextOccurrence_ReturnsNullForImpossibleSchedules()
    {
        // Feb 30 never exists -- the scanner must give up after a year, not loop forever.
        CronSchedule.TryParse("0 0 30 2 *", out var schedule);

        Assert.Null(schedule!.NextOccurrence(At(2026, 1, 1, 0, 0)));
    }
}
