using System.Text.Json;
using System.Text.Json.Serialization;
using LunarWard.Wine;

namespace LunarWard.Launch;

/// <summary>
/// 一个游戏档案（<c>config.json</c> 的 <c>profiles.&lt;id&gt;</c>）。
/// </summary>
public sealed class ProfileConfig
{
    /// <summary><c>genshin</c> | <c>zzz</c>。</summary>
    public string Game { get; set; } = "genshin";

    /// <summary><c>China</c> | <c>Global</c>。</summary>
    public string Edition { get; set; } = "China";

    public string Path { get; set; } = "";

    /// <summary><c>Official</c> | <c>Bilibili</c>（仅原神国服）。</summary>
    public string Channel { get; set; } = "Official";

    public PrefixScopeDto Prefix { get; set; } = new() { Kind = "Own", Path = "" };

    public ScopedRuntimeDto Runtime { get; set; } = new() { Kind = "Default" };

    public WineSettings Wine { get; set; } = new();

    public EnhancementSettings Enhancements { get; set; } = new();

    public SandboxSettings Sandbox { get; set; } = new();

    public List<string> Args { get; set; } = [];

    public Dictionary<string, string> Env { get; set; } = new(StringComparer.Ordinal);

    public string? CommandOverride { get; set; }

    /// <summary>有序包装器链（外层在前），例如 <c>["gamemoderun"]</c>。</summary>
    public List<string> Wrappers { get; set; } = [];

    public List<ExternalProgram> ExternalPrograms { get; set; } = [];

    /// <summary>
    /// 额外的截图文件夹（Starward <c>GetExternalScreenshotFolder</c>）。
    /// 图库会把游戏截图目录与这些目录里的图片合并显示，文件名重复时以先出现的为准。
    /// </summary>
    public List<string> ScreenshotFolders { get; set; } = [];

    /// <summary>高级项：<c>AwaitReady</c> 的 grace 毫秒数（默认 6000）。</summary>
    public int ReadyGraceMs { get; set; } = 6000;

    /// <summary>高级项：<c>AwaitImage</c> 超时秒数（默认 120）。</summary>
    public int WaitImageTimeoutSeconds { get; set; } = 120;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }


    /// <summary>展开 <c>~</c> 与相对路径。</summary>
    public static string ExpandPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        var expanded = path.Trim();
        if (expanded == "~" || expanded.StartsWith("~/", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = home + expanded[1..];
        }

        return System.IO.Path.GetFullPath(expanded);
    }
}


/// <summary>沙箱设置（预留；首期只记录不实施 namespace 隔离）。</summary>
public sealed class SandboxSettings
{
    public bool Enabled { get; set; }

    public bool IsolateHome { get; set; } = true;
}


/// <summary>prefix 作用域的 JSON 形态。</summary>
public sealed class PrefixScopeDto
{
    /// <summary><c>Own</c> | <c>Shared</c> | <c>ProtonCompatData</c>。</summary>
    public string Kind { get; set; } = "Own";

    public string? Path { get; set; }

    public string? Name { get; set; }


    public PrefixScope ToDomain() => Kind switch
    {
        "Shared" when !string.IsNullOrEmpty(Name) => new PrefixScope.Shared(Name),
        "ProtonCompatData" when !string.IsNullOrEmpty(Path) => new PrefixScope.ProtonCompatData(Path),
        _ => new PrefixScope.Own(Path ?? ""),
    };


    public static PrefixScopeDto FromDomain(PrefixScope scope) => scope switch
    {
        PrefixScope.Shared shared => new PrefixScopeDto { Kind = "Shared", Name = shared.Name },
        PrefixScope.ProtonCompatData compat => new PrefixScopeDto { Kind = "ProtonCompatData", Path = compat.Path },
        PrefixScope.Own own => new PrefixScopeDto { Kind = "Own", Path = own.Path },
        _ => new PrefixScopeDto { Kind = "Own" },
    };
}


/// <summary>运行时引用的 JSON 形态（四级作用域）。</summary>
public sealed class ScopedRuntimeDto
{
    /// <summary><c>PerGame</c> | <c>Shared</c> | <c>Default</c> | <c>Custom</c>。</summary>
    public string Kind { get; set; } = "Default";

    public string? Name { get; set; }

    public RuntimeConfigDto? Value { get; set; }


    public Scoped<RuntimeConfig> ToDomain() => Kind switch
    {
        "Shared" when !string.IsNullOrEmpty(Name) => new Scoped<RuntimeConfig>.Shared(Name),
        "PerGame" when Value is not null => new Scoped<RuntimeConfig>.PerGame(Value.ToDomain()),
        "Custom" when Value is not null => new Scoped<RuntimeConfig>.Custom(Value.ToDomain()),
        _ => new Scoped<RuntimeConfig>.Default(),
    };


    public static ScopedRuntimeDto FromDomain(Scoped<RuntimeConfig> scope) => scope switch
    {
        Scoped<RuntimeConfig>.Shared shared => new ScopedRuntimeDto { Kind = "Shared", Name = shared.Name },
        Scoped<RuntimeConfig>.PerGame perGame => new ScopedRuntimeDto
        {
            Kind = "PerGame", Value = RuntimeConfigDto.FromDomain(perGame.Value),
        },
        Scoped<RuntimeConfig>.Custom custom => new ScopedRuntimeDto
        {
            Kind = "Custom", Value = RuntimeConfigDto.FromDomain(custom.Value),
        },
        _ => new ScopedRuntimeDto { Kind = "Default" },
    };
}


/// <summary>
/// prefix 解析：把 <see cref="PrefixScope"/> 变成具体路径。
/// </summary>
public static class PrefixResolver
{
    public static string Resolve(PrefixScope scope, string profileId, Platform.IXdgPaths paths)
    {
        switch (scope)
        {
            case PrefixScope.Own own:
                return string.IsNullOrEmpty(own.Path)
                    ? System.IO.Path.Join(paths.PrefixesDirectory, profileId)
                    : ProfileConfig.ExpandPath(own.Path);

            case PrefixScope.Shared shared:
                return System.IO.Path.Join(paths.PrefixesDirectory, shared.Name);

            case PrefixScope.ProtonCompatData compat:
                // 强制 prefix = <path>/pfx，并禁止重装，避免污染 Steam 的记录
                return System.IO.Path.Join(ProfileConfig.ExpandPath(compat.Path), "pfx");

            default:
                return System.IO.Path.Join(paths.PrefixesDirectory, profileId);
        }
    }
}
