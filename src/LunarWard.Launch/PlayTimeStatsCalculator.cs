namespace LunarWard.Launch;

/// <summary>
/// 单局游戏会话（毫秒时间戳），与 <see cref="PlaySession" /> 解耦，便于直接测试。
/// </summary>
/// <param name="Start">会话开始（Unix 毫秒）。</param>
/// <param name="End">会话结束（Unix 毫秒）。</param>
public readonly record struct PlayTimeSpan(long Start, long End)
{
    /// <summary>该会话时长（毫秒）；结束早于开始时为 0。</summary>
    public long Duration => Math.Max(0, End - Start);
}


/// <summary>
/// 游戏时长统计（对应 Starward <c>PlayTimeStatsDialog.LoadPlayTimeStats</c> 的派生逻辑）。
/// </summary>
/// <remarks>
/// <para>只做纯计算：输入会话区间，输出卡片数字与图表数据。数据来源是
/// <see cref="PlayTimeService" />（<c>playtime.json</c>）里已结束的会话。</para>
/// <para>与 Starward 一致：总时长 / 启动次数（会话数）/ 每日平均 / 游玩天数 /
/// 最长连续天数 / 最长单次 / 单日最长 / 上一次游玩（关闭 60 秒后的那次）。</para>
/// </remarks>
public sealed class PlayTimeStats
{
    /// <summary>"上一次游玩"要求会话结束超过 60 秒（Starward 同值）。</summary>
    public const long LastPlayDelayMs = 60_000;


    private readonly Dictionary<DateOnly, long> _perDay = [];


    private PlayTimeStats(IReadOnlyList<PlayTimeSpan> sessions, DateTimeOffset now)
    {
        Sessions = sessions;
        long nowMs = now.ToUnixTimeMilliseconds();

        long longestSpan = 0;
        long longestStart = 0;
        long lastStart = 0;
        long lastSpan = 0;

        foreach (PlayTimeSpan session in sessions)
        {
            long span = session.Duration;
            TotalMilliseconds += span;
            if (span > longestSpan)
            {
                longestSpan = span;
                longestStart = session.Start;
            }

            if (session.Start > lastStart && nowMs - session.End > LastPlayDelayMs)
            {
                lastStart = session.Start;
                lastSpan = span;
            }

            AddToDays(session);
        }

        LongestSpanMilliseconds = longestSpan;
        LongestSpanStart = longestSpan > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(longestStart).LocalDateTime
            : null;
        LastPlayStart = lastStart > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastStart).LocalDateTime
            : null;
        LastPlayMilliseconds = lastStart > 0 ? Math.Max(lastSpan, LastPlayDelayMs) : 0;
        ComputeDerived();
    }


    /// <summary>会话数（= 启动次数）。</summary>
    public IReadOnlyList<PlayTimeSpan> Sessions { get; }


    /// <summary>总游戏时长（毫秒）。</summary>
    public long TotalMilliseconds { get; }


    /// <summary>最长单次游玩（毫秒）。</summary>
    public long LongestSpanMilliseconds { get; }


    /// <summary>最长单次游玩的开始日期（无会话为 null）。</summary>
    public DateTime? LongestSpanStart { get; }


    /// <summary>上一次游玩的开始时间（无会话为 null）。</summary>
    public DateTime? LastPlayStart { get; }


    /// <summary>上一次游玩时长（毫秒，最少 60 秒；无会话为 0）。</summary>
    public long LastPlayMilliseconds { get; }


    /// <summary>游玩天数。</summary>
    public int PlayDays => _perDay.Count;


    /// <summary>每日平均时长（毫秒；无数据为 0）。</summary>
    public long AverageDayMilliseconds { get; private set; }


    /// <summary>最长连续游玩天数。</summary>
    public int LongestStreakDays { get; private set; }


    /// <summary>最长连续游玩的起止日期文本（无数据为空串）。</summary>
    public string LongestStreakText { get; private set; } = "";


    /// <summary>单日最长游玩（毫秒）。</summary>
    public long MaxDayMilliseconds { get; private set; }


    /// <summary>单日最长游玩的日期（无数据为 null）。</summary>
    public DateOnly? MaxDayDate { get; private set; }


    /// <summary>某一天的游玩毫秒数（缺失为 0）。</summary>
    public long MillisecondsOn(DateOnly day) => _perDay.GetValueOrDefault(day);


    /// <summary>按日期升序的每日时长。</summary>
    public IReadOnlyDictionary<DateOnly, long> PerDay => _perDay;


    /// <summary>从 <see cref="PlayTimeService" /> 的会话记录构建统计（只取指定 profile 且已结束的会话）。</summary>
    public static PlayTimeStats FromSessions(IEnumerable<PlaySession> sessions, string profileId, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        var spans = sessions
            .Where(x => string.Equals(x.ProfileId, profileId, StringComparison.Ordinal))
            .Where(x => x.EndedAt > x.StartedAt)
            .OrderBy(x => x.StartedAt)
            .Select(x => new PlayTimeSpan(x.StartedAt.ToUnixTimeMilliseconds(), x.EndedAt.ToUnixTimeMilliseconds()))
            .ToList();
        return new PlayTimeStats(spans, now ?? DateTimeOffset.Now);
    }


    /// <summary>从毫秒时间戳构建（测试用）。</summary>
    public static PlayTimeStats FromSpans(IEnumerable<PlayTimeSpan> sessions, DateTimeOffset? now = null)
        => new(sessions.OrderBy(x => x.Start).ToList(), now ?? DateTimeOffset.Now);


    private void AddToDays(PlayTimeSpan session)
    {
        DateTime startLocal = DateTimeOffset.FromUnixTimeMilliseconds(session.Start).LocalDateTime;
        DateTime endLocal = DateTimeOffset.FromUnixTimeMilliseconds(session.End).LocalDateTime;

        for (DateTime day = startLocal.Date; day <= endLocal.Date; day = day.AddDays(1))
        {
            DateTime dayStart = day == startLocal.Date ? startLocal : day;
            DateTime dayEnd = day == endLocal.Date ? endLocal : day.AddDays(1).AddTicks(-1);
            long duration = (long)Math.Max(0, (dayEnd - dayStart).TotalMilliseconds);
            DateOnly date = DateOnly.FromDateTime(day);
            _perDay[date] = _perDay.GetValueOrDefault(date) + duration;
        }
    }


    private void ComputeDerived()
    {
        if (_perDay.Count > 0)
        {
            AverageDayMilliseconds = TotalMilliseconds / _perDay.Count;
        }

        var ordered = _perDay.Keys.OrderBy(x => x).ToList();
        DateOnly? streakStart = null;
        DateOnly? previous = null;
        int streak = 0;
        foreach (DateOnly day in ordered)
        {
            if (previous is { } p && day == p.AddDays(1))
            {
                streak++;
            }
            else
            {
                streakStart = day;
                streak = 1;
            }

            if (streak > LongestStreakDays)
            {
                LongestStreakDays = streak;
                LongestStreakText = $"{streakStart:yyyy/MM/dd} - {day:yyyy/MM/dd}";
            }

            previous = day;
        }

        foreach ((DateOnly day, long ms) in _perDay)
        {
            if (ms > MaxDayMilliseconds)
            {
                MaxDayMilliseconds = ms;
                MaxDayDate = day;
            }
        }
    }
}
