namespace LunarWard.Wine;

/// <summary>
/// 作用域解析：把 <see cref="Scoped{T}"/> 引用的运行时解析成具体配置。
/// </summary>
/// <remarks>
/// 解析顺序：<c>PerGame | Custom</c> &gt; <c>Shared(name)</c> &gt; <c>Default</c> &gt; 系统兜底。
/// 结果必须可解释（<see cref="ResolvedRuntime.Explain"/>），见 <c>docs/04-FEATURES.md</c> §3.1。
/// </remarks>
public sealed class RuntimeResolver
{
    private readonly IReadOnlyDictionary<string, RuntimeConfig> _runtimes;

    private readonly RuntimeConfig? _defaultRuntime;

    private readonly RunnerResolver _runnerResolver;

    private readonly ComponentRepository _components;

    private readonly Platform.IXdgPaths _paths;


    public RuntimeResolver(
        IReadOnlyDictionary<string, RuntimeConfig> runtimes,
        string? defaultRuntimeName,
        RunnerResolver runnerResolver,
        ComponentRepository components,
        Platform.IXdgPaths paths)
    {
        _runtimes = runtimes;
        _defaultRuntime = defaultRuntimeName is not null && runtimes.TryGetValue(defaultRuntimeName, out var cfg) ? cfg : null;
        _runnerResolver = runnerResolver;
        _components = components;
        _paths = paths;
    }


    /// <summary>
    /// 解析一个作用域引用。失败抛 <see cref="RuntimeNotFoundException"/>。
    /// </summary>
    /// <param name="scope">profile 上的运行时引用。</param>
    /// <param name="explainPrefix">解释串前缀，例如 <c>profile</c>。</param>
    public ResolvedRuntime Resolve(Scoped<RuntimeConfig> scope, string explainPrefix = "profile")
    {
        var (config, name, source) = ResolveConfig(scope);

        var (wine, runnerDir, runnerEnv, runnerExplain) = _runnerResolver.Resolve(config.Runner);

        string? dxvkDir = null;
        var dxvkEnv = new Dictionary<string, string>(StringComparer.Ordinal);
        switch (config.Dxvk)
        {
            case DxvkSource.Managed managed:
                dxvkDir = Path.Join(_paths.DxvkDirectory, managed.Component);
                if (_components.Find("dxvk", managed.Component) is { Entry.Features.Env: { } dxvkFeatures })
                {
                    foreach (var (k, v) in dxvkFeatures)
                    {
                        dxvkEnv[k] = v;
                    }
                }

                break;
            case DxvkSource.Custom custom:
                dxvkDir = custom.Path;
                break;
        }

        return new ResolvedRuntime
        {
            Name = name,
            Runner = config.Runner,
            WineBinary = wine,
            RunnerDirectory = runnerDir,
            Dxvk = config.Dxvk,
            DxvkDirectory = dxvkDir,
            RunnerEnv = runnerEnv,
            DxvkEnv = dxvkEnv,
            Explain = $"{explainPrefix} → {FriendlySource(source)} › {runnerExplain}",
        }.WithSync(config.Sync);
    }


    private static string FriendlySource(string source) => source switch
    {
        "profile（独占）" => "本档案独占",
        "profile（自定义）" => "自定义",
        "shared" => "共用运行时",
        "default" => "默认运行时",
        "system" => "系统兜底",
        _ => source,
    };


    private (RuntimeConfig Config, string Name, string Source) ResolveConfig(Scoped<RuntimeConfig> scope)
    {
        switch (scope)
        {
            case Scoped<RuntimeConfig>.PerGame perGame:
                return (perGame.Value, "per-game", "profile（独占）");

            case Scoped<RuntimeConfig>.Custom custom:
                return (custom.Value, "custom", "profile（自定义）");

            case Scoped<RuntimeConfig>.Shared shared:
                if (_runtimes.TryGetValue(shared.Name, out var sharedConfig))
                {
                    return (sharedConfig, shared.Name, "shared");
                }

                // 具名运行时不存在：退到默认，而不是直接失败
                return ResolveDefault($"shared '{shared.Name}' 不存在");

            case Scoped<RuntimeConfig>.Default:
                return ResolveDefault(null);

            default:
                return ResolveDefault(null);
        }
    }


    private (RuntimeConfig Config, string Name, string Source) ResolveDefault(string? note)
    {
        if (_defaultRuntime is not null)
        {
            return (_defaultRuntime, "default", note is null ? "default" : $"default（{note}，已回退）");
        }

        // 系统兜底
        return (new RuntimeConfig { Runner = new RunnerSource.System(), Dxvk = new DxvkSource.System() },
            "system", note is null ? "系统兜底" : $"系统兜底（{note}）");
    }
}


internal static class ResolvedRuntimeExtensions
{
    /// <summary>把 <see cref="WineSync"/> 追加到解释串（供 UI 一行展示）。</summary>
    public static ResolvedRuntime WithSync(this ResolvedRuntime runtime, WineSync sync)
    {
        var text = sync switch
        {
            WineSync.None => "",
            _ => $" · sync={sync}",
        };
        return runtime with { Explain = runtime.Explain + text };
    }
}
