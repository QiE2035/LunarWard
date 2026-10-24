using System.Text.Json;
using System.Text.Json.Serialization;
using LunarWard.Wine;

namespace LunarWard.Launch;

/// <summary>
/// 启动器全局配置（<c>$XDG_CONFIG_HOME/lunarward/config.json</c>）。
/// </summary>
/// <remarks>
/// 写入规则见 <c>docs/02-ARCHITECTURE.md</c> §6.3：
/// 原子写、解析失败备份为 <c>.bad-&lt;ts&gt;</c> 并重建默认、<c>schema_version</c> 迁移、
/// 未知字段用 <see cref="ExtensionData"/> 承接。
/// </remarks>
public sealed class LauncherConfig
{
    public const int CurrentSchemaVersion = 1;


    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("launcher")]
    public LauncherSettings Launcher { get; set; } = new();

    [JsonPropertyName("runtimes")]
    public Dictionary<string, RuntimeConfigDto> Runtimes { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("profiles")]
    public Dictionary<string, ProfileConfig> Profiles { get; set; } = new(StringComparer.Ordinal);

    /// <summary>未知字段保留，避免旧版本启动器抹掉新版本写的字段。</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }


    private static readonly JsonSerializerOptions Options = CreateOptions();


    private static JsonSerializerOptions CreateOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };


    /// <summary>
    /// 读取配置；损坏时备份并重建默认。
    /// </summary>
    public static LauncherConfig Load(string path, out string? warning)
    {
        warning = null;
        if (!File.Exists(path))
        {
            return new LauncherConfig();
        }

        try
        {
            var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(path), Options) ?? new LauncherConfig();
            if (config.SchemaVersion > CurrentSchemaVersion)
            {
                warning = $"配置文件 schema_version={config.SchemaVersion} 高于本版本（{CurrentSchemaVersion}），未知字段已保留。";
            }

            return config;
        }
        catch (JsonException ex)
        {
            var backup = $"{path}.bad-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            try
            {
                File.Move(path, backup, overwrite: true);
            }
            catch (IOException)
            {
                // ignore
            }

            warning = $"配置文件解析失败（{ex.Message}），已备份为 {backup} 并重建默认配置。";
            var fresh = new LauncherConfig();
            fresh.Save(path);
            return fresh;
        }
    }


    /// <summary>原子写入。</summary>
    public void Save(string path)
    {
        SchemaVersion = CurrentSchemaVersion;
        Platform.AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }


    public string ToJson() => JsonSerializer.Serialize(this, Options);
}


/// <summary><c>launcher</c> 段。</summary>
public sealed class LauncherSettings
{
    public string Language { get; set; } = "zh-cn";

    /// <summary><c>Nothing</c> | <c>Hide</c> | <c>Close</c>（沿用 Starward）。</summary>
    public string Behavior { get; set; } = "Hide";

    /// <summary><c>Modern</c> | <c>Classic</c>。</summary>
    public string Style { get; set; } = "Modern";

    public bool VideoBackground { get; set; } = true;

    /// <summary>
    /// 启动器右下角显示游戏账号切换条（对应 Starward <c>AppConfig.EnableGameAccountSwitcher</c>，
    /// 默认关闭）。开启后还要本机保存过该游戏的账号才会出现。
    /// </summary>
    public bool EnableGameAccountSwitcher { get; set; }

    public int BackgroundIndex { get; set; }

    public string? DefaultRuntime { get; set; }

    /// <summary>启动器默认的游戏特化开关（未在 profile 覆盖时使用）。</summary>
    public int LastProfileIndex { get; set; }


    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}


/// <summary>
/// 运行时（<c>runtimes.&lt;name&gt;</c>）的 JSON 形态。
/// </summary>
/// <remarks>
/// <see cref="RunnerSource"/> / <see cref="DxvkSource"/> 是 <c>abstract record</c>，
/// 这里用可空字段 + <c>kind</c> 判别，避免自定义多态序列化带来的脆弱性。
/// </remarks>
public sealed class RuntimeConfigDto
{
    public RunnerDto Runner { get; set; } = new() { Kind = "System" };

    public DxvkDto Dxvk { get; set; } = new() { Kind = "System" };

    public WineSync Sync { get; set; } = WineSync.None;

    public Dictionary<string, string> Env { get; set; } = new(StringComparer.Ordinal);

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }


    public RuntimeConfig ToDomain() => new()
    {
        Runner = Runner.ToDomain(),
        Dxvk = Dxvk.ToDomain(),
        Sync = Sync,
        Env = new Dictionary<string, string>(Env, StringComparer.Ordinal),
    };


    public static RuntimeConfigDto FromDomain(RuntimeConfig config) => new()
    {
        Runner = RunnerDto.FromDomain(config.Runner),
        Dxvk = DxvkDto.FromDomain(config.Dxvk),
        Sync = config.Sync,
        Env = new Dictionary<string, string>(config.Env, StringComparer.Ordinal),
    };
}


public sealed class RunnerDto
{
    /// <summary><c>Managed</c> | <c>System</c> | <c>Proton</c> | <c>Custom</c>。</summary>
    public string Kind { get; set; } = "System";

    public string? Component { get; set; }

    public string? SteamLibraryPath { get; set; }

    public string? ProtonName { get; set; }

    public string? Path { get; set; }

    public WineLoader Loader { get; set; } = WineLoader.Auto;


    public RunnerSource ToDomain() => Kind switch
    {
        "Managed" when !string.IsNullOrEmpty(Component) => new RunnerSource.Managed(Component),
        "Proton" when !string.IsNullOrEmpty(SteamLibraryPath) && !string.IsNullOrEmpty(ProtonName)
            => new RunnerSource.Proton(SteamLibraryPath, ProtonName),
        "Custom" when !string.IsNullOrEmpty(Path) => new RunnerSource.Custom(Path, Loader),
        _ => new RunnerSource.System(),
    };


    public static RunnerDto FromDomain(RunnerSource source) => source switch
    {
        RunnerSource.Managed managed => new RunnerDto { Kind = "Managed", Component = managed.Component },
        RunnerSource.Proton proton => new RunnerDto
        {
            Kind = "Proton", SteamLibraryPath = proton.SteamLibraryPath, ProtonName = proton.ProtonName,
        },
        RunnerSource.Custom custom => new RunnerDto { Kind = "Custom", Path = custom.Path, Loader = custom.Loader },
        _ => new RunnerDto { Kind = "System" },
    };
}


public sealed class DxvkDto
{
    /// <summary><c>Managed</c> | <c>System</c> | <c>Builtin</c> | <c>Custom</c>。</summary>
    public string Kind { get; set; } = "System";

    public string? Component { get; set; }

    public string? Path { get; set; }


    public DxvkSource ToDomain() => Kind switch
    {
        "Managed" when !string.IsNullOrEmpty(Component) => new DxvkSource.Managed(Component),
        "Builtin" => new DxvkSource.Builtin(),
        "Custom" when !string.IsNullOrEmpty(Path) => new DxvkSource.Custom(Path),
        _ => new DxvkSource.System(),
    };


    public static DxvkDto FromDomain(DxvkSource source) => source switch
    {
        DxvkSource.Managed managed => new DxvkDto { Kind = "Managed", Component = managed.Component },
        DxvkSource.Builtin => new DxvkDto { Kind = "Builtin" },
        DxvkSource.Custom custom => new DxvkDto { Kind = "Custom", Path = custom.Path },
        _ => new DxvkDto { Kind = "System" },
    };
}
