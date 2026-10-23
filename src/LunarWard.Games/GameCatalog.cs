namespace LunarWard.Games;

/// <summary>游戏版本（国服 / 国际服）。</summary>
public enum GameEdition
{
    /// <summary>国服（<c>YuanShen.exe</c> / <c>ZenlessZoneZero.exe</c>）。</summary>
    China,

    /// <summary>国际服（<c>GenshinImpact.exe</c> / <c>ZenlessZoneZero.exe</c>）。</summary>
    Global,
}


/// <summary>
/// 一个"游戏 + 版本"的静态描述。只有静态事实（文件名、目录名、GameBiz），
/// 不含任何用户配置或运行时状态。
/// </summary>
/// <remarks>
/// 见 <c>docs/04-FEATURES.md</c> §2.7：ZZZ 没有 B 站渠道，所以
/// <see cref="SupportedChannels"/> 按游戏区分。
/// </remarks>
public sealed record GameDefinition(
    string Kind,
    string DisplayName,
    GameEdition Edition,
    string Executable,
    string DataDirectory,
    string GameBiz,
    IReadOnlyList<GameChannel> SupportedChannels,
    IReadOnlyList<string> DefaultArguments)
{
    /// <summary>Unity 的插件目录（B 站 <c>PCGameSDK.dll</c> 装在这里）。</summary>
    public string PluginsRelativePath => Path.Join(DataDirectory, "Plugins");

    /// <summary>B 站插件相对游戏目录的路径。</summary>
    public string BilibiliPluginRelativePath => Path.Join(PluginsRelativePath, BilibiliPluginDownloader.PluginFileName);

    /// <summary>B 站插件绝对路径。</summary>
    public string GetBilibiliPluginPath(string gameDirectory) => Path.Join(gameDirectory, BilibiliPluginRelativePath);

    /// <summary>游戏可执行文件绝对路径。</summary>
    public string GetExecutablePath(string gameDirectory) => Path.Join(gameDirectory, Executable);

    /// <summary><c>config.ini</c> 绝对路径。</summary>
    public string GetConfigIniPath(string gameDirectory) => Path.Join(gameDirectory, "config.ini");

    /// <summary>是否国服（B 站渠道的必要条件之一）。</summary>
    public bool IsChina => Edition == GameEdition.China;

    /// <summary>该定义支持的渠道（UI 按这个隐藏选项）。</summary>
    public IReadOnlyList<GameChannel> GetSupportedChannels() => SupportedChannels;

    /// <summary>渠道是否被该游戏支持。</summary>
    public bool Supports(GameChannel channel) => SupportedChannels.Contains(channel);

    /// <summary>
    /// 唯一标识（<c>genshin-cn</c> / <c>zzz-global</c>），用于字典键与日志。
    /// </summary>
    public string Id => $"{Kind}-{(Edition == GameEdition.China ? "cn" : "global")}";

    /// <summary>对应的 <see cref="LunarWard.Core.GameBiz"/>。</summary>
    public LunarWard.Core.GameBiz ToGameBiz() => new(GameBiz);

    public override string ToString() => $"{DisplayName}（{Edition}）";
}


/// <summary>
/// 游戏静态目录。<b>只放静态事实</b>：没有 IO、没有用户配置。
/// </summary>
public static class GameCatalog
{
    /// <summary>原神 · 国服。</summary>
    public static readonly GameDefinition GenshinChina = new(
        Kind: GameCatalog.Genshin,
        DisplayName: "原神",
        Edition: GameEdition.China,
        Executable: "YuanShen.exe",
        DataDirectory: "YuanShen_Data",
        GameBiz: "hk4e_cn",
        SupportedChannels: [GameChannel.Official, GameChannel.Bilibili],
        DefaultArguments: []);

    /// <summary>原神 · 国际服。</summary>
    public static readonly GameDefinition GenshinGlobal = new(
        Kind: GameCatalog.Genshin,
        DisplayName: "原神",
        Edition: GameEdition.Global,
        Executable: "GenshinImpact.exe",
        DataDirectory: "GenshinImpact_Data",
        GameBiz: "hk4e_global",
        SupportedChannels: [GameChannel.Official],
        DefaultArguments: []);

    /// <summary>绝区零 · 国服。</summary>
    public static readonly GameDefinition ZzzChina = new(
        Kind: GameCatalog.Zzz,
        DisplayName: "绝区零",
        Edition: GameEdition.China,
        Executable: "ZenlessZoneZero.exe",
        DataDirectory: "ZenlessZoneZero_Data",
        GameBiz: "nap_cn",
        SupportedChannels: [GameChannel.Official],
        DefaultArguments: []);

    /// <summary>绝区零 · 国际服。</summary>
    public static readonly GameDefinition ZzzGlobal = new(
        Kind: GameCatalog.Zzz,
        DisplayName: "绝区零",
        Edition: GameEdition.Global,
        Executable: "ZenlessZoneZero.exe",
        DataDirectory: "ZenlessZoneZero_Data",
        GameBiz: "nap_global",
        SupportedChannels: [GameChannel.Official],
        DefaultArguments: []);


    /// <summary>游戏种类标识：原神。</summary>
    public const string Genshin = "genshin";

    /// <summary>游戏种类标识：绝区零。</summary>
    public const string Zzz = "zzz";


    /// <summary>全部定义（顺序即 UI 顺序）。</summary>
    public static IReadOnlyList<GameDefinition> All { get; } =
    [
        GenshinChina,
        GenshinGlobal,
        ZzzChina,
        ZzzGlobal,
    ];


    /// <summary>全部游戏种类标识。</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Genshin, Zzz];


    /// <summary>
    /// 按 <paramref name="kind"/>（<c>genshin</c> | <c>zzz</c>，忽略大小写）与
    /// <paramref name="edition"/> 查定义；未知 kind 返回 <c>null</c>。
    /// </summary>
    public static GameDefinition? Get(string kind, GameEdition edition)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return null;
        }

        return All.FirstOrDefault(x =>
            x.Edition == edition && x.Kind.Equals(kind.Trim(), StringComparison.OrdinalIgnoreCase));
    }


    /// <summary>按 <paramref name="kind"/> 取国服定义（未知 kind 返回 <c>null</c>）。</summary>
    public static GameDefinition? GetChina(string kind) => Get(kind, GameEdition.China);


    /// <summary>按 <paramref name="kind"/> 取国际服定义（未知 kind 返回 <c>null</c>）。</summary>
    public static GameDefinition? GetGlobal(string kind) => Get(kind, GameEdition.Global);


    /// <summary>某个 kind 支持的全部版本。</summary>
    public static IReadOnlyList<GameDefinition> AllFor(string kind)
        => All.Where(x => x.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)).ToList();


    /// <summary>
    /// 从游戏目录推断定义：先按目录里的 exe 名匹配，再按 <c>config.ini</c> 的
    /// <c>game_biz</c>，最后按数据目录名。全都匹配不到返回 <c>null</c>。
    /// </summary>
    public static GameDefinition? Detect(string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            return null;
        }

        foreach (var definition in All)
        {
            if (File.Exists(definition.GetExecutablePath(gameDirectory)))
            {
                return definition;
            }
        }

        try
        {
            var config = GameConfigIniFile.ReadFromGameDirectory(gameDirectory);
            var biz = config.General.GameBiz.Value;
            var byBiz = All.FirstOrDefault(x => x.GameBiz.Equals(biz, StringComparison.OrdinalIgnoreCase));
            if (byBiz is not null)
            {
                return byBiz;
            }
        }
        catch (Exception)
        {
            // 忽略：继续用目录名兜底
        }

        foreach (var definition in All)
        {
            if (Directory.Exists(Path.Join(gameDirectory, definition.DataDirectory)))
            {
                return definition;
            }
        }

        return null;
    }


    /// <summary>按 <see cref="LunarWard.Core.GameBiz"/> 找定义。</summary>
    public static GameDefinition? FromGameBiz(LunarWard.Core.GameBiz gameBiz)
        => All.FirstOrDefault(x => x.GameBiz.Equals(gameBiz.Value, StringComparison.OrdinalIgnoreCase));


    /// <summary>kind 的 UI 名称（未知 kind 返回原串）。</summary>
    public static string GetDisplayName(string kind)
        => kind.ToLowerInvariant() switch
        {
            Genshin => "原神",
            Zzz => "绝区零",
            _ => kind,
        };
}
