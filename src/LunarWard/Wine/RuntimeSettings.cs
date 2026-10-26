namespace LunarWard.Wine;

/// <summary>
/// 一个具名运行时的配置（对应 <c>config.json</c> 的 <c>runtimes.&lt;name&gt;</c>）。
/// </summary>
public sealed class RuntimeConfig
{
    public RunnerSource Runner { get; set; } = new RunnerSource.System();

    public DxvkSource Dxvk { get; set; } = new DxvkSource.System();

    public WineSync Sync { get; set; } = WineSync.None;

    /// <summary>用户配置的额外环境变量（层 4）。</summary>
    public Dictionary<string, string> Env { get; set; } = new(StringComparer.Ordinal);
}


/// <summary>provider 的 wine 设置（层 5）。</summary>
public sealed class WineSettings
{
    public WineSync Sync { get; set; } = WineSync.None;

    /// <summary><c>System</c> | <c>zh-cn</c> | <c>en-us</c> …；影响 <c>LANG</c>。</summary>
    public string Language { get; set; } = "System";

    public bool Borderless { get; set; }

    /// <summary>winewayland 模式：需要 <c>DISPLAY=""</c>（存在且为空），不是删除。</summary>
    public bool WineWayland { get; set; }

    /// <summary>补丁 <c>WINE_ENABLE_TIMEOUT_FIX</c>。</summary>
    public bool TimeoutFix { get; set; }

    /// <summary>需要注册 <c>WINEDLLOVERRIDES</c> 的 DLL 列表（层 5）。</summary>
    public List<string> SharedLibraries { get; set; } = [];
}


/// <summary>增强设置（层 6）。</summary>
public sealed class EnhancementSettings
{
    public bool Fsr { get; set; }

    /// <summary><c>None</c> | <c>Fps</c> | <c>Full</c> | 自定义串。</summary>
    public string Hud { get; set; } = "None";

    public bool Dx12 { get; set; }

    public bool Vkd3d { get; set; }
}


/// <summary>
/// 环境变量来源标记。用于 <c>EnvSources</c>、UI 展示与启动报告。
/// </summary>
public static class EnvLayer
{
    public const string Baseline = "baseline";
    public const string Runner = "runner";
    public const string Dxvk = "dxvk";
    public const string RuntimeEnv = "runtime.env";
    public const string WineSettings = "wine";
    public const string Enhancement = "enhancements";
    public const string ProfileEnv = "profile.env";
    public const string GameAdapter = "game";
    public const string ExternalProgram = "external-program";
    public const string Wrapper = "wrapper";
}


/// <summary>
/// 十层环境变量合并器。层号越小优先级越低。
/// </summary>
/// <remarks>见 <c>docs/03-LAUNCH-ENGINE.md</c> §6。</remarks>
public sealed class EnvironmentBuilder
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sources = new(StringComparer.Ordinal);


    /// <summary>应用一层。<paramref name="overwrite"/> 为 <c>false</c> 时不覆盖已有键。</summary>
    public EnvironmentBuilder Apply(string layer, IReadOnlyDictionary<string, string>? values, bool overwrite = true)
    {
        if (values is null)
        {
            return this;
        }

        foreach (var (key, value) in values)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!overwrite && _values.ContainsKey(key))
            {
                continue;
            }

            _values[key] = value;
            _sources[key] = layer;
        }

        return this;
    }


    /// <summary>设置单个键。</summary>
    public EnvironmentBuilder Set(string layer, string key, string value)
    {
        _values[key] = value;
        _sources[key] = layer;
        return this;
    }


    /// <summary>删除键（用于三态里的 <c>Remove</c>）。</summary>
    public EnvironmentBuilder Remove(string key)
    {
        _values.Remove(key);
        _sources.Remove(key);
        return this;
    }


    public bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);


    public IReadOnlyDictionary<string, string> Values => _values;

    public IReadOnlyDictionary<string, string> Sources => _sources;


    /// <summary>克隆一份（外部程序需要基于游戏 env 再做三态补丁）。</summary>
    public EnvironmentBuilder Clone()
    {
        var clone = new EnvironmentBuilder();
        foreach (var (k, v) in _values)
        {
            clone._values[k] = v;
        }

        foreach (var (k, v) in _sources)
        {
            clone._sources[k] = v;
        }

        return clone;
    }
}
