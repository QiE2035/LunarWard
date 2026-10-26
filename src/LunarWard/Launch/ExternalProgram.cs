using LunarWard.Wine;

namespace LunarWard.Launch;

/// <summary>七相位。见 <c>docs/03-LAUNCH-ENGINE.md</c> §2。</summary>
public enum Phase
{
    /// <summary>写 config.ini、映射 drive、清理 prefix、写模式文件。</summary>
    Prepare,

    /// <summary>Unix 侧 spawn <c>wine &lt;exe&gt;</c> —— 第一个客户。</summary>
    Launch,

    /// <summary>等游戏镜像被映射进 <b>本 prefix</b> 的进程。</summary>
    AwaitImage,

    /// <summary>等反作弊/Unity 初始化过关。</summary>
    AwaitReady,

    /// <summary>第二客户登场：外部程序。</summary>
    PostLaunch,

    /// <summary>监视退出。</summary>
    Running,

    /// <summary>清理残留、回写 sessions、写报告。</summary>
    Cleanup,
}


/// <summary><c>AwaitReady</c> 命中的信号。</summary>
public enum ReadySignal
{
    /// <summary>保护驱动宿主 <c>winedevice.exe</c> 已启动 —— <b>最强</b>。</summary>
    WineDevice,

    /// <summary>Unity 已开始初始化（<c>Player.log</c> 的 mtime 已更新）—— 强。</summary>
    PlayerLog,

    /// <summary>超时兜底 —— 最弱，可能过早。</summary>
    Grace,
}


/// <summary>外部程序种类。</summary>
public enum ProgramKind
{
    /// <summary>走 wine 拉起。</summary>
    WindowsExe,

    /// <summary>直接 exec。</summary>
    UnixBinary,
}


/// <summary>
/// 触发方式。<b>只有两个值</b>：不提供"游戏启动前运行程序"的能力，
/// 那正是破坏"首个 wine 客户端"不变式的行为。
/// </summary>
public enum Trigger
{
    /// <summary>伴随启动：游戏 Ready 后自动拉起 —— 这就是"补丁注入"。</summary>
    Auto,

    /// <summary>手动：用户在 UI 里点"启动"才拉起 —— 这就是"后启动"。</summary>
    Manual,
}


/// <summary>游戏退出后如何处理外部程序。</summary>
public enum ExitPolicy
{
    /// <summary>保留（用户明确要求）。</summary>
    Keep,

    /// <summary>终止（默认，避免污染下次启动）。</summary>
    Kill,
}


/// <summary>环境变量三态。<c>Override("")</c> 与 <c>Remove</c> 语义不同。</summary>
public enum EnvPatchKind
{
    /// <summary>不写，继承游戏启动时的值。</summary>
    Inherit,

    /// <summary>设成 <c>Value</c>（空字符串也是合法值）。</summary>
    Override,

    /// <summary>从继承结果里删掉这个键。</summary>
    Remove,
}


/// <summary>一条环境变量补丁。</summary>
public sealed class EnvEntry
{
    public required string Key { get; set; }

    public EnvPatchKind Kind { get; set; } = EnvPatchKind.Inherit;

    /// <summary><c>Kind == Override</c> 时生效，<b>允许空字符串</b>。</summary>
    public string? Value { get; set; }
}


/// <summary>通用文件写入指令（补丁模式文件等）。</summary>
public sealed class FileWriteSpec
{
    /// <summary>支持 <c>%keywords%</c>。</summary>
    public required string Path { get; set; }

    /// <summary>支持 <c>%keywords%</c>。</summary>
    public string Content { get; set; } = "";

    public FileWriteWhen When { get; set; } = FileWriteWhen.Prepare;
}


public enum FileWriteWhen
{
    Prepare,
    AfterGameReady,
}


/// <summary>
/// 外部程序。后启动与补丁注入是<b>同一个概念</b>，靠 <see cref="Trigger"/> 区分。
/// </summary>
public sealed class ExternalProgram
{
    public required string Id { get; set; }

    /// <summary>用户自由命名。</summary>
    public string Name { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public ProgramKind Kind { get; set; } = ProgramKind.WindowsExe;

    /// <summary>用户手动选，绝对路径或 <c>%keywords%</c>。</summary>
    public required string Path { get; set; }

    public List<string> Args { get; set; } = [];

    /// <summary><c>null</c> = 继承游戏目录。</summary>
    public string? WorkingDirectory { get; set; }

    public List<EnvEntry> Env { get; set; } = [];

    public Trigger Trigger { get; set; } = Trigger.Manual;

    /// <summary>是否前台等待。</summary>
    public bool Wait { get; set; }

    public ExitPolicy OnGameExit { get; set; } = ExitPolicy.Kill;

    public List<FileWriteSpec> PreWrites { get; set; } = [];


    /// <summary>应用三态 env 补丁到一份继承结果上，返回新字典。</summary>
    public Dictionary<string, string> ApplyEnv(IReadOnlyDictionary<string, string> inherited)
    {
        var env = new Dictionary<string, string>(inherited, StringComparer.Ordinal);
        foreach (var entry in Env)
        {
            switch (entry.Kind)
            {
                case EnvPatchKind.Inherit:
                    break;
                case EnvPatchKind.Override:
                    // 空字符串必须保留——对 wine 而言"存在但为空"≠"不存在"
                    env[entry.Key] = entry.Value ?? "";
                    break;
                case EnvPatchKind.Remove:
                    env.Remove(entry.Key);
                    break;
            }
        }

        return env;
    }


    /// <summary>
    /// 启动前的安全提示（非阻止）。见 <c>docs/04-FEATURES.md</c> §1.6。
    /// </summary>
    public IEnumerable<ProgramWarning> GetWarnings(IReadOnlyDictionary<string, string?> keywords)
    {
        if (Trigger == Trigger.Auto && Kind == ProgramKind.WindowsExe)
        {
            yield return new ProgramWarning(Id, WarningSeverity.Warning,
                "该程序会在游戏就绪后被拉起，成为该 wine prefix 的第二个客户端。"
                + "若它需要破坏游戏的保护期初始化，可能导致游戏闪退。");
        }

        if (Trigger == Trigger.Auto && Wait)
        {
            yield return new ProgramWarning(Id, WarningSeverity.Info,
                "该程序前台等待，游戏退出前不会返回。");
        }

        var resolved = KeywordReplacer.Replace(Path, keywords);
        if (!string.IsNullOrEmpty(resolved) && !File.Exists(resolved))
        {
            yield return new ProgramWarning(Id, WarningSeverity.Error, $"文件不存在：{resolved}");
        }

        if (resolved.Contains("%game%", StringComparison.OrdinalIgnoreCase) && PreWrites.Count > 0)
        {
            yield return new ProgramWarning(Id, WarningSeverity.Warning,
                "正在向游戏目录写入文件，可能被视为修改游戏文件。");
        }

        foreach (var write in PreWrites)
        {
            var target = KeywordReplacer.Replace(write.Path, keywords);
            if (target.Replace('\\', '/').Contains("/%game%", StringComparison.OrdinalIgnoreCase)
                || (keywords.TryGetValue("game", out var gameDir)
                    && gameDir is not null
                    && System.IO.Path.GetFullPath(target).StartsWith(System.IO.Path.GetFullPath(gameDir), StringComparison.Ordinal)))
            {
                yield return new ProgramWarning(Id, WarningSeverity.Warning,
                    $"写入目标在游戏目录内：{target}");
            }
        }
    }


    /// <summary>启发式建议（仅 UI，不落盘）。见 <c>docs/04-FEATURES.md</c> §1.7。</summary>
    public string? GetSuggestion()
    {
        var name = System.IO.Path.GetFileName(Path);
        return name switch
        {
            _ when name.Contains("inject", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("touch", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("hook", StringComparison.OrdinalIgnoreCase)
                => "看起来是注入类程序，建议设为『伴随启动』",
            _ when name.Contains("map", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("wiki", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("tool", StringComparison.OrdinalIgnoreCase)
                => "看起来是辅助工具，建议设为『手动启动』",
            _ => null,
        };
    }
}


public enum WarningSeverity
{
    Info,
    Warning,
    Error,
}


public sealed record ProgramWarning(string ProgramId, WarningSeverity Severity, string Message);


/// <summary>
/// 拉起一个外部程序的完整计划（<see cref="LaunchPlan"/> 展开后的结果）。
/// </summary>
public sealed record ExternalProgramPlan
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required ProgramKind Kind { get; init; }

    public required string Executable { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public required string WorkingDirectory { get; init; }

    public required IReadOnlyDictionary<string, string> Env { get; init; }

    public required Trigger Trigger { get; init; }

    public required bool Wait { get; init; }

    public required ExitPolicy OnGameExit { get; init; }

    public required IReadOnlyList<FileWriteSpec> PreWrites { get; init; }
}
