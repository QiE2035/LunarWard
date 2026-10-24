using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// <see cref="PlayTimeStats" />：启动器时长统计弹窗的派生数据
/// （对应 Starward <c>PlayTimeStatsDialog.LoadPlayTimeStats</c>）。
/// </summary>
public sealed class PlayTimeStatsTests
{
    private static DateTimeOffset At(int year, int month, int day, int hour = 12, int minute = 0)
        => new(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local));


    private static PlaySession Session(string profile, DateTimeOffset start, TimeSpan duration)
        => new(profile, start, start + duration, 0, null);


    [Fact]
    public void Empty_data_has_zero_totals()
    {
        var stats = PlayTimeStats.FromSpans([]);

        Assert.Equal(0, stats.TotalMilliseconds);
        Assert.Equal(0, stats.PlayDays);
        Assert.Equal(0, stats.LongestStreakDays);
        Assert.Null(stats.LastPlayStart);
        Assert.Equal("", stats.LongestStreakText);
    }


    [Fact]
    public void Totals_count_sessions_and_days()
    {
        var now = At(2026, 10, 24, 20);
        var stats = PlayTimeStats.FromSpans(
        [
            new PlayTimeSpan(At(2026, 10, 20, 10).ToUnixTimeMilliseconds(),
                At(2026, 10, 20, 12).ToUnixTimeMilliseconds()),
            new PlayTimeSpan(At(2026, 10, 21, 10).ToUnixTimeMilliseconds(),
                At(2026, 10, 21, 13).ToUnixTimeMilliseconds()),
        ], now);

        Assert.Equal(TimeSpan.FromHours(5).TotalMilliseconds, stats.TotalMilliseconds);
        Assert.Equal(2, stats.Sessions.Count);
        Assert.Equal(2, stats.PlayDays);
        Assert.Equal(TimeSpan.FromMinutes(150).TotalMilliseconds, stats.AverageDayMilliseconds);
        Assert.Equal(TimeSpan.FromHours(3).TotalMilliseconds, stats.LongestSpanMilliseconds);
        Assert.Equal(At(2026, 10, 21, 10).LocalDateTime, stats.LongestSpanStart);
        Assert.Equal(2, stats.LongestStreakDays);
        Assert.Equal("2026/10/20 - 2026/10/21", stats.LongestStreakText);
    }


    [Fact]
    public void Session_crossing_midnight_splits_over_two_days()
    {
        // 本地 23:00 到次日 01:00 → 两天各 1 小时
        var stats = PlayTimeStats.FromSpans(
        [
            new PlayTimeSpan(At(2026, 10, 20, 23).ToUnixTimeMilliseconds(),
                At(2026, 10, 21, 1).ToUnixTimeMilliseconds()),
        ], At(2026, 10, 24));

        Assert.Equal(2, stats.PlayDays);
        // 跨天拆分按 Starward 同款算法（次日零点减 1 tick）计算，整数截断允许几毫秒误差
        Assert.InRange(stats.MillisecondsOn(new DateOnly(2026, 10, 20)), 3_599_000, 3_600_000);
        Assert.InRange(stats.MillisecondsOn(new DateOnly(2026, 10, 21)), 3_599_000, 3_600_000);
    }


    [Fact]
    public void Streak_breaks_on_gap()
    {
        var stats = PlayTimeStats.FromSpans(
        [
            new PlayTimeSpan(At(2026, 10, 1).ToUnixTimeMilliseconds(), At(2026, 10, 1, 13).ToUnixTimeMilliseconds()),
            new PlayTimeSpan(At(2026, 10, 2).ToUnixTimeMilliseconds(), At(2026, 10, 2, 13).ToUnixTimeMilliseconds()),
            new PlayTimeSpan(At(2026, 10, 5).ToUnixTimeMilliseconds(), At(2026, 10, 5, 13).ToUnixTimeMilliseconds()),
        ], At(2026, 10, 24));

        Assert.Equal(2, stats.LongestStreakDays);
        Assert.Equal("2026/10/01 - 2026/10/02", stats.LongestStreakText);
    }


    [Fact]
    public void Last_play_ignores_session_that_ended_within_60_seconds()
    {
        var now = At(2026, 10, 24, 20);
        var running = new PlayTimeSpan(now.ToUnixTimeMilliseconds() - 30_000, now.ToUnixTimeMilliseconds() - 10_000);
        var earlier = new PlayTimeSpan(
            now.AddHours(-5).ToUnixTimeMilliseconds(), now.AddHours(-4).ToUnixTimeMilliseconds());

        var stats = PlayTimeStats.FromSpans([running, earlier], now);

        Assert.Equal(now.AddHours(-5).LocalDateTime, stats.LastPlayStart);
        Assert.Equal(TimeSpan.FromHours(1).TotalMilliseconds, stats.LastPlayMilliseconds);
    }


    [Fact]
    public void Last_play_has_a_60_second_floor()
    {
        var now = At(2026, 10, 24, 20);
        var shortSession = new PlayTimeSpan(
            now.AddHours(-2).ToUnixTimeMilliseconds(), now.AddHours(-2).ToUnixTimeMilliseconds() + 5_000);

        var stats = PlayTimeStats.FromSpans([shortSession], now);

        Assert.Equal(60_000, stats.LastPlayMilliseconds);
    }


    [Fact]
    public void FromSessions_filters_by_profile_and_finished_only()
    {
        var sessions = new[]
        {
            Session("genshin-cn", At(2026, 10, 20, 10), TimeSpan.FromHours(1)),
            Session("zzz-cn", At(2026, 10, 20, 10), TimeSpan.FromHours(2)),
            new PlaySession("genshin-cn", At(2026, 10, 21, 10), At(2026, 10, 21, 9), null, null), // 结束早于开始：丢弃
        };

        var stats = PlayTimeStats.FromSessions(sessions, "genshin-cn", At(2026, 10, 24));

        Assert.Single(stats.Sessions);
        Assert.Equal(TimeSpan.FromHours(1).TotalMilliseconds, stats.TotalMilliseconds);
    }


    [Fact]
    public void Max_day_is_reported()
    {
        var stats = PlayTimeStats.FromSpans(
        [
            new PlayTimeSpan(At(2026, 10, 1, 10).ToUnixTimeMilliseconds(), At(2026, 10, 1, 11).ToUnixTimeMilliseconds()),
            new PlayTimeSpan(At(2026, 10, 2, 10).ToUnixTimeMilliseconds(), At(2026, 10, 2, 16).ToUnixTimeMilliseconds()),
        ], At(2026, 10, 24));

        Assert.Equal(TimeSpan.FromHours(6).TotalMilliseconds, stats.MaxDayMilliseconds);
        Assert.Equal(new DateOnly(2026, 10, 2), stats.MaxDayDate);
    }
}
