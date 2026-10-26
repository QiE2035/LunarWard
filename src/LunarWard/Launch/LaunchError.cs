namespace LunarWard.Launch;

/// <summary>启动错误。UI 层按类型给出不同的可交互提示。</summary>
/// <remarks>见 <c>docs/02-ARCHITECTURE.md</c> §7。</remarks>
public abstract record LaunchError
{
    /// <summary>退出码语义见 <c>docs/03-LAUNCH-ENGINE.md</c> §9。</summary>
    public abstract int ExitCode { get; }

    public abstract string Message { get; }


    /// <summary>prefix 没有清理干净（还有本 prefix 的 wine 客户端）。</summary>
    public sealed record PrefixNotClean(IReadOnlyList<int> Pids) : LaunchError
    {
        public override int ExitCode => 4;

        public override string Message =>
            $"wine prefix 里仍有 {Pids.Count} 个客户端进程（pid: {string.Join(", ", Pids)}），无法保证游戏是首个客户端。";
    }


    public sealed record GameNotInstalled(string Path) : LaunchError
    {
        public override int ExitCode => 1;

        public override string Message => $"游戏未安装或路径无效：{Path}";
    }


    public sealed record RuntimeNotFound(string Name, string Detail) : LaunchError
    {
        public override int ExitCode => 1;

        public override string Message => $"运行时 '{Name}' 不可用：{Detail}";
    }


    /// <summary>如 <c>PCGameSDK.dll</c> 缺失。</summary>
    public sealed record ChannelPrerequisiteMissing(string Detail) : LaunchError
    {
        public override int ExitCode => 1;

        public override string Message => $"渠道前置条件不满足：{Detail}";
    }


    /// <summary>游戏在启动期退出（多半是保护期被破坏）。</summary>
    public sealed record GameExitedDuringStartup(int ExitCodeValue, string? LogPath, string? Hint = null) : LaunchError
    {
        public override int ExitCode => 2;

        public override string Message =>
            $"游戏在启动期退出（退出码 {ExitCodeValue}）。"
            + (Hint is null ? "" : Hint)
            + (LogPath is null ? "" : $"（日志：{LogPath}）");

        public override string ToString() => Message;
    }


    public sealed record InjectorBinaryMissing(string Path) : LaunchError
    {
        public override int ExitCode => 3;

        public override string Message => $"外部程序不存在：{Path}";
    }


    public sealed record ExternalProgramFailed(string Id, int ExitCodeValue) : LaunchError
    {
        public override int ExitCode => 3;

        public override string Message => $"外部程序 '{Id}' 启动失败，退出码 {ExitCodeValue}。";
    }


    /// <summary>预检失败（参数错误、目录不存在等）。</summary>
    public sealed record Preflight(string Detail) : LaunchError
    {
        public override int ExitCode => 1;

        public override string Message => $"预检失败：{Detail}";
    }
}


/// <summary>一次启动的最终结果。</summary>
public sealed record LaunchResult
{
    public required bool Success { get; init; }

    public LaunchError? Error { get; init; }

    /// <summary>退出码（0 = 成功）。</summary>
    public int ExitCode => Error?.ExitCode ?? 0;

    /// <summary>七相位的耗时记录。</summary>
    public IReadOnlyList<PhaseRecord> Phases { get; init; } = [];

    public IReadOnlyList<ExternalProgramRecord> ExternalPrograms { get; init; } = [];

    /// <summary>游戏进程 pid（若找到）。</summary>
    public int? GamePid { get; init; }

    /// <summary><c>AwaitReady</c> 命中的信号。</summary>
    public ReadySignal? Signal { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? ExitedAt { get; init; }

    public int? GameExitCode { get; init; }


    public static LaunchResult Fail(LaunchError error) => new() { Success = false, Error = error };
}
