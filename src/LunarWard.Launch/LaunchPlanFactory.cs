using LunarWard.Wine;

namespace LunarWard.Launch;

/// <summary>
/// 游戏侧的启动信息。由 <c>LunarWard.Games</c> 的 <c>GameCatalog</c>/<c>GameAdapter</c>
/// 提供，避免 <c>LunarWard.Launch</c> 反向依赖 <c>LunarWard.Games</c>。
/// </summary>
public sealed record GameLaunchInfo
{
    /// <summary><c>genshin</c> | <c>zzz</c>。</summary>
    public required string Kind { get; init; }

    /// <summary>游戏可执行文件名，例如 <c>YuanShen.exe</c>。同时用作 <c>AwaitImage</c> 的镜像名。</summary>
    public required string Executable { get; init; }

    /// <summary>数据目录名，例如 <c>YuanShen_Data</c>（用于定位 <c>PCGameSDK.dll</c>）。</summary>
    public required string DataDirectory { get; init; }

    /// <summary>GameBiz 字符串，例如 <c>hk4e_cn</c>。</summary>
    public string GameBiz { get; init; } = "";

    /// <summary>游戏默认启动参数（在 profile.Args 之前）。</summary>
    public IReadOnlyList<string> DefaultArguments { get; init; } = [];

    /// <summary>游戏特化 env（第 8 层）。</summary>
    public IReadOnlyDictionary<string, string> GameEnv { get; init; } = new Dictionary<string, string>();

    /// <summary>游戏特化附加参数（例如 borderless）。</summary>
    public IReadOnlyList<string> ExtraArguments { get; init; } = [];


    /// <summary>
    /// 无 <c>LunarWard.Games</c> 时的最小兜底（按 kind + edition 猜）。
    /// 仅用于测试与"游戏目录里只有一个 exe"的简陋场景。
    /// </summary>
    public static GameLaunchInfo Fallback(string kind, string edition)
    {
        var china = string.Equals(edition, "China", StringComparison.OrdinalIgnoreCase);
        return kind switch
        {
            "zzz" => new GameLaunchInfo
            {
                Kind = "zzz",
                Executable = "ZenlessZoneZero.exe",
                DataDirectory = "ZenlessZoneZero_Data",
                GameBiz = china ? "nap_cn" : "nap_global",
            },
            _ => new GameLaunchInfo
            {
                Kind = "genshin",
                Executable = china ? "YuanShen.exe" : "GenshinImpact.exe",
                DataDirectory = china ? "YuanShen_Data" : "GenshinImpact_Data",
                GameBiz = china ? "hk4e_cn" : "hk4e_global",
            },
        };
    }
}


/// <summary>
/// 把 <see cref="ProfileConfig"/> 组装成不可变的 <see cref="LaunchPlan"/>。
/// </summary>
/// <remarks>
/// 十层环境变量的<b>唯一</b>实现。环境 shell 与真实启动必须都经过这里，
/// 否则"调试环境和实际启动环境不一致"，shell 就失去意义。
/// </remarks>
public sealed class LaunchPlanFactory
{
    private readonly RuntimeResolver _runtimeResolver;

    private readonly Platform.IXdgPaths _paths;


    public LaunchPlanFactory(RuntimeResolver runtimeResolver, Platform.IXdgPaths paths)
    {
        _runtimeResolver = runtimeResolver;
        _paths = paths;
    }


    /// <summary>
    /// 构建启动计划。<paramref name="gameInfo"/> 由调用方（Games 层）提供。
    /// </summary>
    /// <exception cref="RuntimeNotFoundException">运行时不可用。</exception>
    /// <exception cref="FileNotFoundException">游戏 exe 不存在。</exception>
    public LaunchPlan Build(
        string profileId,
        ProfileConfig profile,
        GameLaunchInfo gameInfo,
        string? defaultRuntimeName = null,
        bool dryRun = false)
    {
        var gameDirectory = ProfileConfig.ExpandPath(profile.Path);
        var prefix = PrefixResolver.Resolve(profile.Prefix.ToDomain(), profileId, _paths);

        // ── 运行时（含四级作用域解析）──
        var runtime = _runtimeResolver.Resolve(profile.Runtime.ToDomain(), $"profile '{profileId}'");

        // ── 十层 env ──
        var env = new EnvironmentBuilder();

        // 1. 基线
        env.Set(EnvLayer.Baseline, "WINEPREFIX", prefix);
        env.Set(EnvLayer.Baseline, "WINEARCH", "win64");
        env.Set(EnvLayer.Baseline, "WINEDEBUG", "-all");
        env.Set(EnvLayer.Baseline, "WINEDLLOVERRIDES", "winemenubuilder.exe=d");
        env.Set(EnvLayer.Baseline, "WINEESYNC", "1");

        // 2. runner 组件
        env.Apply(EnvLayer.Runner, runtime.RunnerEnv);

        // 3. dxvk 组件
        env.Apply(EnvLayer.Dxvk, runtime.DxvkEnv);

        // 4. runtime.env
        var runtimeConfig = profile.Runtime.ToDomain() switch
        {
            Scoped<RuntimeConfig>.PerGame pg => pg.Value,
            Scoped<RuntimeConfig>.Custom c => c.Value,
            _ => null,
        };
        if (runtimeConfig is not null)
        {
            env.Apply(EnvLayer.RuntimeEnv, runtimeConfig.Env);
        }

        // 5. wine 设置
        env.Apply(EnvLayer.WineSettings, BuildWineSettingsEnv(profile.Wine));

        // 6. 增强
        env.Apply(EnvLayer.Enhancement, BuildEnhancementEnv(profile.Enhancements));

        // 7. profile.env
        env.Apply(EnvLayer.ProfileEnv, profile.Env);

        // 8. 游戏特化
        env.Apply(EnvLayer.GameAdapter, gameInfo.GameEnv);

        // 9/10. 外部程序 env（三态）在"每个程序"上单独应用，不污染游戏 env

        // ── 关键字表 ──
        var keywords = new KeywordReplacer.Builder()
            .Build(runtime.RunnerDirectory)
            .Prefix(prefix)
            .Game(gameDirectory)
            .Launcher(_paths.DataHome)
            .Temp(Path.GetTempPath())
            .Exe(gameInfo.Executable)
            .Channel(profile.Channel)
            .Runtime(runtime.Name)
            .Build();

        // ── 启动参数 ──
        var arguments = new List<string> { gameInfo.Executable };
        arguments.AddRange(gameInfo.DefaultArguments);
        arguments.AddRange(gameInfo.ExtraArguments);
        arguments.AddRange(KeywordReplacer.Replace(profile.Args, keywords));

        // ── 外部程序 ──
        var externalPrograms = BuildExternalPrograms(profile.ExternalPrograms, keywords, env.Values, runtime);

        var plan = new LaunchPlan
        {
            ProfileId = profileId,
            GameKind = gameInfo.Kind,
            GameDirectory = gameDirectory,
            GameImage = gameInfo.Executable,
            Runtime = runtime,
            Prefix = prefix,
            WineBinary = runtime.WineBinary,
            Arguments = arguments,
            Env = env.Values,
            EnvSources = env.Sources,
            ExternalPrograms = externalPrograms,
            CommandTemplate = profile.CommandOverride,
            Wrappers = profile.Wrappers.ToList(),
            Keywords = keywords,
            WaitImageTimeout = TimeSpan.FromSeconds(Math.Max(1, profile.WaitImageTimeoutSeconds)),
            ReadyGrace = TimeSpan.FromMilliseconds(Math.Max(0, profile.ReadyGraceMs)),
            DryRun = dryRun,
        };

        return plan;
    }


    /// <summary>层 5：wine 设置 → env。</summary>
    public static Dictionary<string, string> BuildWineSettingsEnv(WineSettings wine)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);

        switch (wine.Sync)
        {
            case WineSync.NTSync:
                env["WINEFSYNC"] = "0";
                env["WINENSYNC"] = "1";
                break;
            case WineSync.ESync:
                env["WINEFSYNC"] = "0";
                env["WINEESYNC"] = "1";
                break;
            case WineSync.FSync:
                env["WINEFSYNC"] = "1";
                break;
            case WineSync.None:
                env["WINEFSYNC"] = "0";
                break;
        }

        if (!string.IsNullOrEmpty(wine.Language) && !string.Equals(wine.Language, "System", StringComparison.OrdinalIgnoreCase))
        {
            env["LANG"] = NormalizeLocale(wine.Language) + ".UTF-8";
        }

        if (wine.SharedLibraries.Count > 0)
        {
            // 环境变量优先级高于注册表（见 docs/04-FEATURES.md §3.7 的说明）
            env["WINEDLLOVERRIDES"] = string.Join(';', wine.SharedLibraries.Select(d => $"{d}=n,b"));
        }

        return env;
    }


    /// <summary>
    /// 把 <c>zh-cn</c> / <c>zh_CN</c> / <c>zh-CN</c> 统一成 <c>zh_CN</c>
    /// （POSIX 语言环境要求语言小写、地区大写）。
    /// </summary>
    public static string NormalizeLocale(string language)
    {
        var text = language.Trim().Replace('-', '_');
        var parts = text.Split('_', 2);
        if (parts.Length == 1)
        {
            return parts[0].ToLowerInvariant();
        }

        return parts[0].ToLowerInvariant() + "_" + parts[1].ToUpperInvariant();
    }


    /// <summary>层 6：增强 → env。</summary>
    public static Dictionary<string, string> BuildEnhancementEnv(EnhancementSettings enhancements)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);

        if (enhancements.Fsr)
        {
            env["WINE_FULLSCREEN_FSR"] = "1";
            env["WINE_FULLSCREEN_FSR_STRENGTH"] = "2";
        }

        if (!string.IsNullOrEmpty(enhancements.Hud) && !string.Equals(enhancements.Hud, "None", StringComparison.OrdinalIgnoreCase))
        {
            env["DXVK_HUD"] = enhancements.Hud switch
            {
                "Fps" => "fps",
                "Full" => "fps,frametimes,devinfo,memory,gpuload",
                _ => enhancements.Hud,
            };
        }

        return env;
    }


    private List<ExternalProgramPlan> BuildExternalPrograms(
        IReadOnlyList<ExternalProgram> programs,
        IReadOnlyDictionary<string, string?> keywords,
        IReadOnlyDictionary<string, string> gameEnv,
        ResolvedRuntime runtime)
    {
        var result = new List<ExternalProgramPlan>();
        foreach (var program in programs.Where(p => p.Enabled))
        {
            var executable = KeywordReplacer.Replace(program.Path, keywords);
            var workingDirectory = string.IsNullOrEmpty(program.WorkingDirectory)
                ? (keywords.TryGetValue("game", out var game) && game is not null ? game : "/")
                : KeywordReplacer.Replace(program.WorkingDirectory, keywords);

            result.Add(new ExternalProgramPlan
            {
                Id = program.Id,
                Name = program.Name,
                Kind = program.Kind,
                Executable = executable,
                Arguments = KeywordReplacer.Replace(program.Args, keywords),
                WorkingDirectory = workingDirectory,
                // 三态合并：以游戏 env 为继承基底
                Env = program.ApplyEnv(gameEnv),
                Trigger = program.Trigger,
                Wait = program.Wait,
                OnGameExit = program.OnGameExit,
                PreWrites = program.PreWrites,
            });
        }

        return result;
    }
}
