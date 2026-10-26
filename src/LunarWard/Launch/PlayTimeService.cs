using LunarWard.Launch.Database;
using LunarWard.Platform;

namespace LunarWard.Launch;

/// <summary>一次已结算的游戏会话（<see cref="PlayTimeRecord"/> 的展示视图）。</summary>
/// <param name="GameBiz">游戏区服，如 <c>hk4e_cn</c>（上游 <c>PlayTimeStats.GameBiz</c>）。</param>
/// <param name="StartedAt">开始时间。</param>
/// <param name="EndedAt">结束时间。</param>
/// <param name="GameExitCode">退出码（库里不存，读取时恒为 null）。</param>
/// <param name="ReadySignal">结束信号（库里不存，读取时恒为 null）。</param>
public sealed record PlaySession(
    string GameBiz,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int? GameExitCode,
    string? ReadySignal)
{
    public TimeSpan Duration => EndedAt - StartedAt;
}


/// <summary>
/// 游戏时长统计（放在 Launch 层：它只消费 <see cref="LaunchResult"/>，不依赖 UI）。
/// </summary>
/// <remarks>
/// <para>
/// 见 <c>docs/05-ROADMAP.md</c> §3.3："游戏时长 —— 复用相位引擎的起止时间"。
/// 相位引擎天然有精确的 <see cref="LaunchResult.StartedAt"/> /
/// <see cref="LaunchResult.ExitedAt"/>，不需要额外的进程监视。
/// </para>
/// <para>
/// 存储：与抽卡记录同一个 SQLite 库 <c>&lt;DataHome&gt;/LunarWardDatabase.db</c>，
/// 表结构照抄 Starward 的 <c>PlayTimeStats</c>（见 <see cref="PlayTimeStore"/>）。
/// 之前这里是自造的 <c>playtime.json</c>，已按「本地记录存储一律与上游一致用 SQLite」改掉。
/// </para>
/// </remarks>
public sealed class PlayTimeService
{
    private readonly object _gate = new();

    private readonly PlayTimeStore _store;


    public PlayTimeService(IXdgPaths paths, PlayTimeStore? store = null)
    {
        _store = store ?? PlayTimeStore.OpenDefault(paths.DataHome);
    }


    /// <summary>某区服的全部会话（按开始时间倒序）。</summary>
    public IReadOnlyList<PlaySession> Sessions(string gameBiz)
    {
        lock (_gate)
        {
            return _store.GetAll(gameBiz)
                .Select(ToSession)
                .OrderByDescending(x => x.StartedAt)
                .ToList();
        }
    }


    /// <summary>某区服的总时长。</summary>
    public TimeSpan TotalFor(string gameBiz)
    {
        lock (_gate)
        {
            return TimeSpan.FromTicks(_store.GetAll(gameBiz).Sum(x => (x.EndedAt - x.StartedAt) * 10_000));
        }
    }


    /// <summary>某区服最近一次会话。</summary>
    public PlaySession? LastFor(string gameBiz)
    {
        lock (_gate)
        {
            PlayTimeRecord? last = _store.GetAll(gameBiz).OrderByDescending(x => x.StartedAt).FirstOrDefault();
            return last is null ? null : ToSession(last);
        }
    }


    /// <summary>
    /// 记一次启动：<paramref name="gameBiz"/> 来自当前档案（如 <c>hk4e_cn</c>），
    /// <paramref name="pid"/> 为游戏进程号（未知时传 0，唯一索引仍按区服+开始时间去重）。
    /// </summary>
    public void Record(string gameBiz, int pid, LaunchResult result)
    {
        if (result.StartedAt is not { } started || result.ExitedAt is not { } exited)
        {
            return;
        }

        if (exited <= started || string.IsNullOrWhiteSpace(gameBiz))
        {
            return;
        }

        lock (_gate)
        {
            // 上游 PlayTimeStats 存 Unix 毫秒；唯一索引 (GameBiz, StartTime, Pid) 保证重复结算不重复
            _store.Save(gameBiz, pid, started.ToUnixTimeMilliseconds(), exited.ToUnixTimeMilliseconds());
        }
    }


    /// <summary>
    /// 记一次「外部启动」的会话（对应上游 <c>playtime --pid</c> 的
    /// <c>PlayTimeRecordService.LogPlayTimeAsync</c>：用进程开始/退出时间补一条统计）。
    /// </summary>
    public void RecordSession(string gameBiz, int pid, DateTimeOffset started, DateTimeOffset exited)
    {
        if (exited <= started || string.IsNullOrWhiteSpace(gameBiz))
        {
            return;
        }

        lock (_gate)
        {
            _store.Save(gameBiz, pid, started.ToUnixTimeMilliseconds(), exited.ToUnixTimeMilliseconds());
        }
    }


    private static PlaySession ToSession(PlayTimeRecord record)
        => new(
            record.GameBiz,
            DateTimeOffset.FromUnixTimeMilliseconds(record.StartedAt),
            DateTimeOffset.FromUnixTimeMilliseconds(record.EndedAt),
            null,
            null);
}
