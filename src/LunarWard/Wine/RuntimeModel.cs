namespace LunarWard.Wine;

/// <summary>
/// 四级作用域。解析顺序：<c>PerGame | Custom</c> &gt; <c>Shared(name)</c> &gt;
/// <c>Default</c> &gt; 系统兜底。
/// </summary>
/// <remarks>见 <c>docs/04-FEATURES.md</c> §3.1。</remarks>
public abstract record Scoped<T>
{
    /// <summary>本 profile 独占。</summary>
    public sealed record PerGame(T Value) : Scoped<T>;

    /// <summary>引用具名共用运行时。</summary>
    public sealed record Shared(string Name) : Scoped<T>;

    /// <summary>跟随启动器全局默认。</summary>
    public sealed record Default : Scoped<T>;

    /// <summary>行内自定义，不落具名表。</summary>
    public sealed record Custom(T Value) : Scoped<T>;


    /// <summary>作用域的人类可读名（用于 UI 与启动报告）。</summary>
    public string KindName => this switch
    {
        PerGame => "PerGame",
        Shared => "Shared",
        Default => "Default",
        Custom => "Custom",
        _ => "Unknown",
    };
}


/// <summary>runner（wine 本体）来源。</summary>
public abstract record RunnerSource
{
    /// <summary>托管组件（从 an-anime-team 组件索引下载）。</summary>
    public sealed record Managed(string Component) : RunnerSource;

    /// <summary>PATH 里的 wine。</summary>
    public sealed record System : RunnerSource;

    /// <summary>Steam 的 Proton。</summary>
    public sealed record Proton(string SteamLibraryPath, string ProtonName) : RunnerSource;

    /// <summary>用户指定路径。</summary>
    public sealed record Custom(string Path, WineLoader Loader = WineLoader.Auto) : RunnerSource;


    public string KindName => this switch
    {
        Managed => "Managed",
        System => "System",
        Proton => "Proton",
        Custom => "Custom",
        _ => "Unknown",
    };
}


/// <summary>自定义 runner 的加载方式。</summary>
public enum WineLoader
{
    /// <summary>自动探测 <c>bin/wine</c> 或文件本身。</summary>
    Auto,

    /// <summary>直接 exec 该二进制。</summary>
    Binary,

    /// <summary>通过 <c>wine64</c> 启动。</summary>
    Wine64,
}


/// <summary>DXVK 来源。</summary>
public abstract record DxvkSource
{
    public sealed record Managed(string Component) : DxvkSource;

    /// <summary>用 wine 自带。</summary>
    public sealed record System : DxvkSource;

    /// <summary>不装任何东西。</summary>
    public sealed record Builtin : DxvkSource;

    public sealed record Custom(string Path) : DxvkSource;


    public string KindName => this switch
    {
        Managed => "Managed",
        System => "System",
        Builtin => "Builtin",
        Custom => "Custom",
        _ => "Unknown",
    };
}


/// <summary>prefix 作用域。</summary>
public abstract record PrefixScope
{
    public sealed record Own(string Path) : PrefixScope;

    public sealed record Shared(string Name) : PrefixScope;

    /// <summary>复用 Steam 的兼容数据目录：强制 <c>prefix = &lt;path&gt;/pfx</c>。</summary>
    public sealed record ProtonCompatData(string Path) : PrefixScope;


    public string KindName => this switch
    {
        Own => "Own",
        Shared => "Shared",
        ProtonCompatData => "ProtonCompatData",
        _ => "Unknown",
    };
}


/// <summary>同步原语。</summary>
public enum WineSync
{
    None,
    NTSync,
    ESync,
    FSync,
}


/// <summary>
/// 运行时解析结果。<b>必须可解释</b>：<see cref="Explain"/> 用于 UI 与启动报告。
/// </summary>
public sealed record ResolvedRuntime
{
    public required string Name { get; init; }

    public required RunnerSource Runner { get; init; }

    /// <summary>解析出的 wine 可执行文件绝对路径。</summary>
    public required string WineBinary { get; init; }

    /// <summary>runner 根目录（<c>%build%</c>）。</summary>
    public required string RunnerDirectory { get; init; }

    public required DxvkSource Dxvk { get; init; }

    /// <summary>dxvk 安装目录（<c>Managed</c>/<c>Custom</c> 时非空）。</summary>
    public string? DxvkDirectory { get; init; }

    /// <summary>runner 组件自带的 env（低优先级层 2）。</summary>
    public IReadOnlyDictionary<string, string> RunnerEnv { get; init; } = new Dictionary<string, string>();

    /// <summary>dxvk 组件自带的 env（层 3）。</summary>
    public IReadOnlyDictionary<string, string> DxvkEnv { get; init; } = new Dictionary<string, string>();

    /// <summary>十层合并里的"来源解释"，例如 <c>profile → shared-cachyos</c>。</summary>
    public required string Explain { get; init; }
}


/// <summary>运行时解析失败。</summary>
public sealed class RuntimeNotFoundException : Exception
{
    public RuntimeNotFoundException(string name, string detail)
        : base($"运行时 '{name}' 不可用：{detail}")
    {
        RuntimeName = name;
        Detail = detail;
    }


    public string RuntimeName { get; }

    public string Detail { get; }
}
