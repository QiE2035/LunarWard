using LunarWard.Wine;

namespace LunarWard.Launch;

/// <summary>
/// 相位引擎的输入。<b>构建后只读</b>——引擎不改它，只读。
/// </summary>
/// <remarks>
/// 这样 <c>launch-report.json</c> 与"环境 shell"拿到的是<b>同一份</b>数据，
/// 不会出现"调试环境和实际启动环境不一致"的问题。
/// 见 <c>docs/03-LAUNCH-ENGINE.md</c> §5。
/// </remarks>
public sealed class LaunchPlan
{
    public required string ProfileId { get; init; }

    /// <summary><c>genshin</c> | <c>zzz</c>。</summary>
    public required string GameKind { get; init; }

    public required string GameDirectory { get; init; }

    /// <summary><c>YuanShen.exe</c> 等。</summary>
    public required string GameImage { get; init; }

    public required ResolvedRuntime Runtime { get; init; }

    public required string Prefix { get; init; }

    /// <summary>从 Unix 侧要执行的 wine 二进制。</summary>
    public required string WineBinary { get; init; }

    /// <summary>传给 wine 的参数（第一个通常是 exe 名，最后一个通常是游戏参数）。</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>完整的环境变量（十层合并结果）。</summary>
    public required IReadOnlyDictionary<string, string> Env { get; init; }

    /// <summary>每个 env 键来自哪一层（用于 UI 展示与排障）。</summary>
    public required IReadOnlyDictionary<string, string> EnvSources { get; init; }

    /// <summary>待拉起的外部程序。</summary>
    public required IReadOnlyList<ExternalProgramPlan> ExternalPrograms { get; init; }

    /// <summary>档位：命令模板（可选，<c>null</c> 表示直接 <c>wine exe args</c>）。</summary>
    public string? CommandTemplate { get; init; }

    /// <summary>有序包装器链（外层在前），例如 <c>["gamemoderun", "mangohud"]</c>。</summary>
    public IReadOnlyList<string> Wrappers { get; init; } = [];

    /// <summary>
    /// <c>gamescope</c> 包装器的参数（LunarWard 的 Linux 扩展）。展开 argv 时插在
    /// <c>gamescope</c> 之后，并自动补一个 <c>--</c> 分隔后续命令。
    /// </summary>
    public IReadOnlyList<string> GamescopeArguments { get; init; } = [];

    /// <summary>关键字表，供外部程序与文件写入替换。</summary>
    public IReadOnlyDictionary<string, string?> Keywords { get; init; } = new Dictionary<string, string?>();

    public TimeSpan WaitImageTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// 保护期长短随游戏/wine 版本漂移，必须可配且暴露到 UI 高级项。默认 6000ms 来自实测。
    /// </summary>
    public TimeSpan ReadyGrace { get; init; } = TimeSpan.FromMilliseconds(6000);

    public TimeSpan RunningSettle { get; init; } = TimeSpan.FromSeconds(30);

    public bool DryRun { get; init; }


    /// <summary>
    /// 展开成最终要执行的 argv（含包装器链）。
    /// </summary>
    public IReadOnlyList<string> BuildArgumentVector()
    {
        var result = new List<string>();
        foreach (var wrapper in Wrappers)
        {
            result.Add(wrapper);
            if (wrapper == "gamescope")
            {
                // gamescope 自己也要吃参数：`gamescope -f -W 2560 -H 1440 -- <内层命令>`。
                // 用户可能自己写了 `--`，这里先剥掉再补一个，避免出现两个分隔符。
                result.AddRange(GamescopeArguments.Where(a => a != "--"));
                result.Add("--");
            }
        }

        result.Add(WineBinary);
        result.AddRange(Arguments);
        return result;
    }


    /// <summary>游戏 pid（<c>Running</c> 之后才可用）；用于 <c>%pid%</c> 关键字。</summary>
    public int? GamePid { get; set; }
}


/// <summary>相位耗时记录（写入启动报告）。</summary>
public sealed record PhaseRecord(string Name, DateTimeOffset Started, long DurationMs)
{
    public string? Detail { get; init; }
}


/// <summary>外部程序启动记录。</summary>
public sealed record ExternalProgramRecord(
    string Id,
    Trigger Trigger,
    string StartedAfter,
    int? Pid,
    int? ExitCode,
    string? Error);
