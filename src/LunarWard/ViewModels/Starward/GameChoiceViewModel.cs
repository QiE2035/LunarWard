using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Services;
using LunarWard.Games;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 游戏选择层的一张卡片（Starward <c>GameBizDisplay</c>）：**每款游戏一张**，
/// 国服 / 国际服 / B 站渠道收进 <see cref="Servers"/>，不再一款游戏出多张卡。
/// </summary>
/// <remarks>
/// 上游 <c>Features/GameSelector/GameBizDisplay.cs</c> 是
/// <c>{ GameInfo GameInfo; List&lt;GameBizIcon&gt; Servers; }</c>；
/// LunarWard 的对应关系：<c>GameInfo</c> → <see cref="Kind"/> + <see cref="Name"/> +
/// 卡片素材（<see cref="Thumbnail"/>/<see cref="Logo"/>），<c>GameBizIcon</c> →
/// <see cref="GameServerItem"/>（档案 id + 渠道 = 一个 gamebiz）。
/// </remarks>
public sealed partial class GameChoiceViewModel : ObservableObject
{
    public GameChoiceViewModel(string kind, string name, IReadOnlyList<GameServerItem> servers,
                               bool isInstalled = false)
    {
        Kind = kind;
        Name = name;
        Servers = servers;
        IsInstalled = isInstalled;
    }


    /// <summary>游戏种类（<c>genshin</c> / <c>zzz</c>），拖放排序的 key。</summary>
    public string Kind { get; }

    /// <summary>游戏中文名（「原神」「绝区零」）。上游国际服卡片同样显示中文名。</summary>
    public string Name { get; }

    /// <summary>该游戏的区服条目（国服官方 / 国际服 / B 站）。</summary>
    public IReadOnlyList<GameServerItem> Servers { get; }

    /// <summary>是否只有一档区服（点卡片直接切换，不弹 flyout）。</summary>
    public bool IsSingleServer => Servers.Count == 1;

    /// <summary>任意一档区服已安装。</summary>
    public bool IsInstalled { get; init; }

    /// <summary>
    /// 用来取卡片素材的 gamebiz：优先当前区服，其次已安装，最后第一档。
    /// </summary>
    public GameServerItem? PrimaryServer
        => Servers.FirstOrDefault(x => x.IsCurrent)
           ?? Servers.FirstOrDefault(x => x.IsInstalled)
           ?? Servers.FirstOrDefault();

    /// <summary>卡片素材对应的 gamebiz（<c>hk4e_cn</c> 等）。</summary>
    public string GameBiz => PrimaryServer?.GameBiz ?? "";

    /// <summary>卡片标题（兜底文案；有 Logo/缩略图时不显示）。</summary>
    public string Title => Name;


    /// <summary>当前游戏（卡片描边/图标条高亮用）。</summary>
    [ObservableProperty]
    private bool _isCurrent;


    /// <summary>40×40 方形游戏图标（卡片 hover 遮罩与图标条用）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private Bitmap? _icon;

    public bool HasIcon => Icon is not null;


    /// <summary>192×108 卡片缩略图。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail), nameof(ShowFallbackTitle))]
    private Bitmap? _thumbnail;

    /// <summary>卡片中央 Logo。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogo), nameof(ShowFallbackTitle))]
    private Bitmap? _logo;

    public bool HasThumbnail => Thumbnail is not null;

    public bool HasLogo => Logo is not null;

    /// <summary>既无缩略图也无 Logo 时显示游戏名。</summary>
    public bool ShowFallbackTitle => !HasThumbnail && !HasLogo;


    /// <summary>XAML 内文案。</summary>
    public Localization.AppStrings Strings => Localization.AppStrings.Instance;
}


/// <summary>
/// 一档区服（Starward <c>GameBizIcon</c>）：<see cref="ProfileId"/> + <see cref="Channel"/>
/// 唯一确定一个档案渠道，也就是上游的一个 <c>GameBiz</c>。
/// </summary>
public sealed partial class GameServerItem : ObservableObject
{
    public GameServerItem(string gameName, string profileId, GameEdition edition, GameChannel channel,
                          string gameBiz, bool isInstalled, bool isPinned)
    {
        GameName = gameName;
        ProfileId = profileId;
        Edition = edition;
        Channel = channel;
        GameBiz = gameBiz;
        IsInstalled = isInstalled;
        _isPinned = isPinned;
    }


    /// <summary>游戏中文名。</summary>
    public string GameName { get; }

    /// <summary>档案 id（<c>genshin-cn</c> / <c>genshin-global</c>）。</summary>
    public string ProfileId { get; }

    public GameEdition Edition { get; }

    public GameChannel Channel { get; }

    /// <summary>渠道对应的 gamebiz（<c>hk4e_cn</c> / <c>hk4e_global</c> / <c>hk4e_bilibili</c>）。</summary>
    public string GameBiz { get; }

    public bool IsInstalled { get; }

    /// <summary>区服显示名（上游 <c>GameBizIcon.ServerName</c>）。</summary>
    public string Name => Channel == GameChannel.Bilibili
        ? "Bilibili"
        : Edition == GameEdition.Global ? "国际服" : "官方国服";

    /// <summary>
    /// 区服角标的文字兜底：上游用打包的 <c>gameicon_hyperion.png</c> /
    /// <c>gameicon_hoyolab.png</c> / <c>gameicon_bilibili.png</c>（见 <see cref="BadgeImage"/>），
    /// 图片还没加载完时先显示同尺寸文字角标。
    /// </summary>
    public string BadgeText => Channel == GameChannel.Bilibili
        ? "B"
        : Edition == GameEdition.Global ? "际" : "国";

    /// <summary>区服角标底色（区分国服 / 国际服 / B 站）。</summary>
    public IBrush BadgeBrush => Channel == GameChannel.Bilibili
        ? BilibiliBrush
        : Edition == GameEdition.Global ? GlobalBrush : ChinaBrush;

    private static readonly IBrush ChinaBrush = new SolidColorBrush(Color.Parse("#FF3D7BD9"));
    private static readonly IBrush GlobalBrush = new SolidColorBrush(Color.Parse("#FF2E9E6B"));
    private static readonly IBrush BilibiliBrush = new SolidColorBrush(Color.Parse("#FFFB7299"));


    /// <summary>配置里的 key（上游 <c>GameBiz</c>，这里用 <c>档案id:渠道</c>）。</summary>
    public string Key => $"{ProfileId}:{Channel}";


    /// <summary>是否被固定在左上角图标条（Starward <c>GameBizIcon.IsPinned</c>）。</summary>
    [ObservableProperty]
    private bool _isPinned;

    /// <summary>是否是当前游戏区服。</summary>
    [ObservableProperty]
    private bool _isCurrent;


    /// <summary>游戏图标（图标条 40×40 与 flyout 里取不到区服图时的兜底）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private Bitmap? _icon;

    public bool HasIcon => Icon is not null;


    /// <summary>
    /// 区服 logo（24×24 flyout / 已安装列表、20×20 图标条与左上角角标），来自
    /// <c>Starward.Assets</c> 内容包的 <c>Assets/Image/gameicon_*.png</c>。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadgeImage))]
    private Bitmap? _badgeImage;

    public bool HasBadgeImage => BadgeImage is not null;


    /// <summary>区服 logo 的文件路径（<see cref="GameAssets.ServerIconPath"/>）。</summary>
    public string BadgeImagePath => GameAssets.ServerIconPath(Edition, Channel);
}


/// <summary>「已安装游戏实际占用大小」的一行（图标 / 游戏名 / 区服 / 容量 / 路径）。</summary>
public sealed partial class InstalledGameItem(string gameName, string serverName, string badgeText, IBrush badgeBrush,
                                              Bitmap? icon, string sizeText, string path) : ObservableObject
{
    public string GameName { get; } = gameName;

    public string ServerName { get; } = serverName;

    public string BadgeText { get; } = badgeText;

    public IBrush BadgeBrush { get; } = badgeBrush;

    public Bitmap? Icon { get; } = icon;

    public string SizeText { get; } = sizeText;

    public string Path { get; } = path;

    public bool HasIcon => Icon is not null;

    /// <summary>区服 logo（24×24，来自 <c>Starward.Assets</c>；加载前显示 <see cref="BadgeText"/>）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadgeImage))]
    private Bitmap? _badgeImage;

    public bool HasBadgeImage => BadgeImage is not null;

    public string ToolTip => $"{GameName} · {ServerName}\n{Path}";
}


/// <summary>
/// 纯函数组装器：档案列表 → 每款游戏一张卡片（Starward
/// <c>InitializeGameServerArea</c> 的语言过滤 + 区服分类）。抽成静态类是为了单测。
/// </summary>
public static class GameChoiceBuilder
{
    /// <summary>简体中文 UI（Starward <c>LanguageUtil.FilterLanguage(...) is "zh-cn"</c>）。</summary>
    public const string ChineseLanguage = "zh-cn";


    /// <summary>
    /// 组装卡片。
    /// </summary>
    /// <param name="profiles">全部档案（国服 / 国际服，顺序无关）。</param>
    /// <param name="uiLanguage">UI 语言（<c>zh-cn</c> / <c>en-us</c>）。</param>
    /// <param name="pinnedServers">已固定的区服 key（<c>档案id:渠道</c>）。</param>
    /// <param name="order">卡片顺序（游戏 kind 列表；兼容旧的 <c>genshin-cn</c> / <c>hk4e_cn</c> 写法）。</param>
    /// <param name="currentProfileId">当前档案 id。</param>
    /// <param name="currentChannel">当前渠道。</param>
    public static IReadOnlyList<GameChoiceViewModel> Build(
        IReadOnlyList<ProfileItemViewModel> profiles,
        string? uiLanguage,
        IReadOnlyCollection<string>? pinnedServers = null,
        IReadOnlyList<string>? order = null,
        string? currentProfileId = null,
        GameChannel currentChannel = GameChannel.Official)
    {
        bool chinese = string.Equals(uiLanguage, ChineseLanguage, StringComparison.OrdinalIgnoreCase);
        var pinned = pinnedServers is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(pinnedServers, StringComparer.Ordinal);
        var profileById = profiles.ToDictionary(x => x.Id, StringComparer.Ordinal);

        var cards = new List<GameChoiceViewModel>();
        foreach (string kind in GameCatalog.Kinds)
        {
            // 语言决定用哪个区服的游戏信息（名字/素材）：简体中文取国服，否则取国际服。
            var display = chinese ? GameCatalog.GetChina(kind) : GameCatalog.GetGlobal(kind);
            if (display is null)
            {
                continue;
            }

            var servers = new List<GameServerItem>();
            // 上游对每张卡按 _cn / _global / _bilibili 三个后缀建区服条目。
            foreach (var edition in (GameEdition[])[GameEdition.China, GameEdition.Global])
            {
                if (GameCatalog.Get(kind, edition) is not { } definition)
                {
                    continue;
                }

                foreach (var channel in definition.GetSupportedChannels())
                {
                    profileById.TryGetValue(definition.Id, out var profile);
                    bool isInstalled = profile?.IsInstalled == true;
                    bool isCurrent = currentProfileId is not null
                                     && string.Equals(definition.Id, currentProfileId, StringComparison.Ordinal)
                                     && channel == currentChannel;
                    servers.Add(new GameServerItem(
                        display.DisplayName, definition.Id, edition, channel,
                        definition.GameBiz, isInstalled, pinned.Contains($"{definition.Id}:{channel}"))
                    {
                        IsCurrent = isCurrent,
                    });
                }
            }

            if (servers.Count == 0)
            {
                continue;
            }

            cards.Add(new GameChoiceViewModel(display.Kind, display.DisplayName, servers)
            {
                IsInstalled = servers.Any(x => x.IsInstalled),
                IsCurrent = servers.Any(x => x.IsCurrent),
            });
        }

        // 顺序：配置里记录的顺序优先，未记录的保持目录默认顺序。
        if (order is { Count: > 0 })
        {
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < order.Count; i++)
            {
                if (NormalizeOrderKey(order[i]) is { } key && !ranks.ContainsKey(key))
                {
                    ranks[key] = i;
                }
            }

            cards = cards.OrderBy(x => ranks.TryGetValue(x.Kind, out int rank) ? rank : int.MaxValue).ToList();
        }

        return cards;
    }


    /// <summary>
    /// 旧的排序 key 是档案 id（<c>genshin-cn</c>）甚至 gamebiz（<c>hk4e_cn</c>），
    /// 新实现按游戏 kind 排序；这里把两种旧写法都归一化。
    /// </summary>
    public static string? NormalizeOrderKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        string lower = key.ToLowerInvariant();
        if (lower.StartsWith("genshin", StringComparison.Ordinal) || lower.StartsWith("hk4e", StringComparison.Ordinal))
        {
            return GameCatalog.Genshin;
        }

        if (lower.StartsWith("zzz", StringComparison.Ordinal) || lower.StartsWith("nap", StringComparison.Ordinal))
        {
            return GameCatalog.Zzz;
        }

        return GameCatalog.Kinds.Contains(lower) ? lower : null;
    }
}
