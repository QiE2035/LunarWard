using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.App.ViewModels;
using LunarWard.App.Views.Starward;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// Starward 外壳（对应 Starward 的 <c>MainView</c>）：左栏导航、游戏选择、背景、
/// 页面 Frame、公告 Toast。
/// </summary>
/// <remarks>
/// <para>
/// 与旧 <see cref="MainWindowViewModel"/> 的分工：外壳只管"导航 + 当前游戏 + 背景"，
/// 启动逻辑仍由 <see cref="Services.LauncherService"/> 提供，启动器页自己的 VM 订阅它。
/// 切换游戏时外壳重建页面（<see cref="PageChanged"/>），页面的 VM 因此可以安全地
/// 在构造时缓存 profile，不需要处理"profile 变了"。
/// </para>
/// <para>布局与像素契约见 <c>docs/07-UI-REPLICA.md</c> §1–§2。</para>
/// </remarks>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;

    private readonly LogService _log;

    private bool _disposed;

    /// <summary>
    /// 素材加载代次。<c>GameChanged</c> 原来会被触发两次（切区服/换游戏时一次、素材加载完再一次），
    /// 每次都会让外壳整页重建，切背景时表现为闪一下并重启视频；这里用代次把两次合并成一次。
    /// </summary>
    private int _artGeneration;

    /// <summary>已经为哪一代触发过 <c>GameChanged</c>；-1 表示还没触发过。</summary>
    private int _lastGameChangedGeneration = -1;


    public ShellViewModel(AppServices services, LogService log)
    {
        _services = services;
        _log = log;
        Assets = new StarwardAssets(services.Paths, log);

        // 启动时恢复「固定图标条」：直接写后台字段，避免 setter 里的 SaveConfig 造成一次多余写盘。
        RestoreGameIconsPinned();

        Profiles = new ObservableCollection<ProfileItemViewModel>();
        ReloadProfiles();

        CurrentPage = ShellPage.Launcher;
        InitializationTask = InitializeAsync();
    }


    // ── 服务 ──────────────────────────────────────────────────────────

    public AppServices Services => _services;

    public StarwardAssets Assets { get; }

    /// <summary>首次素材加载（自检截图与"等首屏就绪"用）。</summary>
    public Task InitializationTask { get; private set; } = Task.CompletedTask;

    public LogService Log => _log;

    public Localization.AppStrings Strings => Localization.AppStrings.Instance;

    /// <summary>当前游戏档案。</summary>
    [ObservableProperty]
    private ProfileItemViewModel _profile = null!;

    public ObservableCollection<ProfileItemViewModel> Profiles { get; }

    /// <summary>游戏选择层的卡片（每款游戏一张，国服/国际服/B 站在卡片内）。</summary>
    public ObservableCollection<GameChoiceViewModel> GameChoices { get; } = [];


    /// <summary>
    /// 左上角横排区服图标条（Starward <c>GameBizIcons</c>）：只放被「固定」的区服。
    /// </summary>
    public ObservableCollection<GameServerItem> PinnedServers { get; } = [];


    /// <summary>当前游戏区服（左上角 40×40 图标对应的 gamebiz）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentServerBadgeText), nameof(CurrentServerBadgeBrush),
        nameof(HasCurrentServer), nameof(ShowCurrentServerBadge))]
    private GameServerItem? _currentServer;


    public string CurrentServerBadgeText => CurrentServer?.BadgeText ?? "";

    public Avalonia.Media.IBrush CurrentServerBadgeBrush
        => CurrentServer?.BadgeBrush ?? Avalonia.Media.Brushes.Transparent;

    public bool HasCurrentServer => CurrentServer is not null;

    /// <summary>
    /// 左上角当前游戏图标右下角的 20×20 区服角标：未固定图标条时才显示
    /// （源码 <c>Visibility={x:Bind IsPinned, Converter=BoolToVisibilityReversedConverter}</c>）。
    /// </summary>
    public bool ShowCurrentServerBadge => HasCurrentServer && !IsGameIconsPinned;


    /// <summary>
    /// 左上角横排区服图标条（Starward GameSelector 的 <c>Grid_GameIconsArea</c>）：
    /// 平时收起（Translation -100），鼠标移到当前游戏图标上时滑出；全屏选择层打开或
    /// 图标条被固定时保持可见。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCurrentServerBadge))]
    private bool _isGameIconsVisible;


    /// <summary>图标条是否被固定常驻（Starward <c>AppConfig.IsGameBizSelectorPinned</c>）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCurrentServerBadge))]
    private bool _isGameIconsPinned;

    /// <summary>启动恢复配置期间为 true：跳过写盘，避免"读出来又原样写回去"。</summary>
    private bool _restoringGameIconsPinned;

    partial void OnIsGameIconsPinnedChanged(bool value)
    {
        if (!_restoringGameIconsPinned)
        {
            _services.Config.Launcher.GameIconsPinned = value;
            SaveConfig();
        }

        if (value)
        {
            IsGameIconsVisible = true;
        }
        else if (!IsGameSelectorOpen)
        {
            IsGameIconsVisible = false;
        }
    }


    /// <summary>
    /// 从 <see cref="Launch.LauncherConfig"/> 恢复图标条固定状态（<c>Launcher.GameIconsPinned</c>）。
    /// 恢复期间跳过 <c>SaveConfig</c>，避免启动时把刚读到的配置再原样写一遍盘。
    /// </summary>
    private void RestoreGameIconsPinned()
    {
        var pinned = _services.Config.Launcher.GameIconsPinned;
        if (pinned == IsGameIconsPinned)
        {
            return;
        }

        _restoringGameIconsPinned = true;
        try
        {
            IsGameIconsPinned = pinned;
        }
        finally
        {
            _restoringGameIconsPinned = false;
        }
    }


    /// <summary>鼠标进入左上角当前游戏图标。</summary>
    public void ShowGameIcons() => IsGameIconsVisible = true;


    /// <summary>鼠标离开图标条。固定或全屏选择层打开时不收起（Starward 同款判断）。</summary>
    public void HideGameIcons()
    {
        if (IsGameSelectorOpen || IsGameIconsPinned)
        {
            return;
        }

        IsGameIconsVisible = false;
    }


    /// <summary>
    /// 拖放排序（Starward <c>DragItemsCompleted</c>）：把 <paramref name="sourceKind"/> 移到
    /// <paramref name="targetIndex"/>，并把顺序写进 <c>LauncherSettings.GameChoiceOrder</c>。
    /// </summary>
    public void MoveGameChoice(string sourceKind, int targetIndex)
    {
        int from = GameChoices.IndexOf(GameChoices.FirstOrDefault(x => x.Kind == sourceKind)!);
        if (from < 0)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, GameChoices.Count - 1);
        if (from == targetIndex)
        {
            return;
        }

        GameChoices.Move(from, targetIndex);
        _services.Config.Launcher.GameChoiceOrder = GameChoices.Select(x => x.Kind).ToList();
        SaveConfig();
    }


    /// <summary>点区服：切到该档案并把渠道写进去（对应源码 <c>Button_GameServer_Click</c>）。</summary>
    [RelayCommand]
    private void SelectServer(GameServerItem? server) => ApplyServer(server);


    /// <summary>「固定」区服（源码 <c>Button_PinGameBiz_Click</c>）。</summary>
    [RelayCommand]
    private void ToggleServerPin(GameServerItem? server)
    {
        if (server is null)
        {
            return;
        }

        server.IsPinned = !server.IsPinned;
        var pinned = _services.Config.Launcher.PinnedGameServers;
        if (server.IsPinned)
        {
            if (!pinned.Contains(server.Key))
            {
                pinned.Add(server.Key);
            }
        }
        else
        {
            pinned.RemoveAll(x => x == server.Key);
        }

        SaveConfig();
        UpdatePinnedServers();
    }


    /// <summary>左上角图标条里点一档已固定的区服：直接切过去。</summary>
    [RelayCommand]
    private void SelectGameIcon(GameServerItem server) => ApplyServer(server);


    /// <summary>全屏选择层里的「固定图标条」开关（源码 <c>Border_Pin</c>）。</summary>
    [RelayCommand]
    private void ToggleGameIconsPinned() => IsGameIconsPinned = !IsGameIconsPinned;


    /// <summary>
    /// 切到某一档区服（档案 + 渠道），并收起选择层/图标条。
    /// 档案不存在时不动作（LunarWard 的四个档案在 <see cref="ReloadProfiles"/> 里总会建好）。
    /// </summary>
    public void ApplyServer(GameServerItem? server)
    {
        if (server is null)
        {
            return;
        }

        IsGameSelectorOpen = false;
        if (Profiles.FirstOrDefault(x => x.Id == server.ProfileId) is not { } profile)
        {
            return;
        }

        string channel = server.Channel.ToString();
        if (!string.Equals(profile.Profile.Channel, channel, StringComparison.Ordinal))
        {
            profile.Profile.Channel = channel;
        }

        SaveConfig();
        Profile = profile;
        _services.Config.Launcher.LastProfileIndex = Profiles.IndexOf(profile);
        SaveConfig();
        MarkCurrentServer();
        var generation = ++_artGeneration;
        _ = LoadArtAsync(generation);
        RaiseGameChanged(generation);
        RebuildPage();
        HideGameIcons();
    }


    // ── 导航 ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLauncherSelected), nameof(IsGameSettingSelected), nameof(IsScreenshotSelected),
        nameof(IsGachaSelected), nameof(IsToolboxSelected), nameof(IsSelfQuerySelected), nameof(IsGenshinBeyondGachaSelected), nameof(IsSettingsSelected),
        nameof(OverlayMaskOpacity))]
    private ShellPage _currentPage;


    public bool IsLauncherSelected => CurrentPage == ShellPage.Launcher;

    /// <summary>
    /// <c>Border_OverlayMask</c> 的不透明度：启动器页为 0（不盖内容），其它页为 1。
    /// 上游 <c>MainView.NavigateTo</c> 的同一判断（<c>page is BlankPage or GameLauncherPage → 0</c>）；
    /// LunarWard 没有「未选游戏」的空页状态，因此 <c>BlankPage</c> 分支不存在。
    /// </summary>
    public double OverlayMaskOpacity => IsLauncherSelected ? 0 : 1;

    public bool IsGameSettingSelected => CurrentPage == ShellPage.GameSetting;

    public bool IsScreenshotSelected => CurrentPage == ShellPage.Screenshot;

    public bool IsGachaSelected => CurrentPage == ShellPage.Gacha;

    public bool IsToolboxSelected => CurrentPage == ShellPage.Toolbox;

    public bool IsSelfQuerySelected => CurrentPage == ShellPage.SelfQuery;

    public bool IsGenshinBeyondGachaSelected => CurrentPage == ShellPage.GenshinBeyondGacha;

    public bool IsSettingsSelected => CurrentPage == ShellPage.Settings;


    /// <summary>页面容器重建通知（参数是新页面的 <see cref="PageContext"/>）。</summary>
    public event EventHandler<PageContext>? PageChanged;

    /// <summary>游戏切换通知（背景、公告卡等需要跟着换）。</summary>
    public event EventHandler? GameChanged;

    public event EventHandler? GameSelectorVisibilityChanged;


    [RelayCommand]
    private void SelectPage(ShellPage page)
    {
        if (CurrentPage == page)
        {
            return;
        }

        CurrentPage = page;
        RebuildPage();
    }


    /// <summary>由页面主动请求跳转（例如设置页里的"诊断"跳到工具箱）。</summary>
    public void Navigate(ShellPage page) => SelectPageCommand.Execute(page);


    /// <summary>
    /// 跳到工具箱的指定 Tab（0 = 每日便笺，1 = 账号记录）。
    /// Starward 的便笺/账号切换是浮层，这里统一落到工具箱页，避免再开独立窗口。
    /// </summary>
    public void NavigateToToolbox(int tab)
    {
        PendingToolboxTab = tab;
        Navigate(ShellPage.Toolbox);
    }


    /// <summary>页面创建后要切到的工具箱 Tab（-1 = 不切）。</summary>
    public int PendingToolboxTab { get; set; } = -1;


    public void RebuildPage() => PageChanged?.Invoke(this, new PageContext
    {
        Services = _services,
        Shell = this,
        Profile = Profile,
        Log = _log,
    });


    /// <summary>触发一次 <c>GameChanged</c> 并记下代次，避免素材加载完成后再触发一次。</summary>
    private void RaiseGameChanged(int generation)
    {
        _lastGameChangedGeneration = generation;
        GameChanged?.Invoke(this, EventArgs.Empty);
    }


    /// <summary>
    /// 左栏展开态（Starward NavigationView 的 <c>PaneDisplayMode="LeftCompact"</c>：
    /// 平时 56px 只显示图标，鼠标移入后展开显示文字）。
    /// </summary>
    /// <summary>
    /// 左栏是否展开。Starward 实际不展开（<c>LeftCompact</c> + 隐藏汉堡键），
    /// 保留该属性只为自检与将来可选的展开模式。
    /// </summary>
    [ObservableProperty]
    private bool _isRailExpanded;


    // ── 游戏选择层 ────────────────────────────────────────────────────

    [ObservableProperty]
    private bool isGameSelectorOpen;

    [ObservableProperty]
    private bool _isGameSelectorLoading;

    partial void OnIsGameSelectorOpenChanged(bool value) => GameSelectorVisibilityChanged?.Invoke(this, EventArgs.Empty);


    /// <summary>
    /// 「已安装游戏实际占用大小」（Starward <c>Expander_InstalledGamesActualSize</c>）：
    /// 列每个已安装档案的目录占用，右下角刷新按钮重算。
    /// </summary>
    [ObservableProperty]
    private string _installedGamesSizeText = "";

    [ObservableProperty]
    private bool _isCalculatingInstalledGames;


    public ObservableCollection<InstalledGameItem> InstalledGameItems { get; } = [];


    /// <summary>多份安装之间重复占用的估算（Starward <c>InstalledGamesSavedSize</c>）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstalledGamesSavedSizeHint))]
    private string? _installedGamesSavedSizeText;


    /// <summary>（其中约 X 可省）——没有重复安装时为空串（Avalonia 的 Run 不支持 IsVisible）。</summary>
    public string InstalledGamesSavedSizeHint
        => string.IsNullOrEmpty(InstalledGamesSavedSizeText)
            ? ""
            : $"（{Strings["Shell.SavedSize"]} {InstalledGamesSavedSizeText}）";


    /// <summary>
    /// 自动查找已安装的游戏（上游 <c>GameSelector.Button_AutoSearchInstalledGames</c>）。
    /// 上游在 Windows 上读 <c>HKCU\Software\miHoYo\HYP\1_1\{gameBiz}</c>、
    /// <c>Cognosphere\HYP\1_0\{gameBiz}</c> 的安装路径；Linux 上没有注册表，这里复用
    /// <see cref="Services.GameScanner"/>（Steam 库 / 常见目录 / sleepy-launcher 与
    /// anime-game-launcher 的配置）扫描到的目录，回填给「同一游戏 + 同一区服、当前目录无效」的档案。
    /// </summary>
    [RelayCommand]
    private async Task AutoSearchInstalledGamesAsync()
    {
        var found = await Task.Run(() => _services.Scanner.Scan()).ConfigureAwait(true);
        int updated = 0;
        foreach (var detected in found)
        {
            foreach (var item in Profiles)
            {
                if (!string.Equals(item.Profile.Game, detected.Definition.Kind, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                bool sameEdition = string.Equals(
                    item.Profile.Edition,
                    detected.Definition.Edition == Games.GameEdition.Global ? "Global" : "China",
                    StringComparison.OrdinalIgnoreCase);
                if (!sameEdition)
                {
                    continue;
                }
                // 已经指向一个存在的目录就不覆盖用户的设置
                if (Directory.Exists(ProfileConfig.ExpandPath(item.Path)))
                {
                    continue;
                }
                if (!string.Equals(item.Path, detected.Directory, StringComparison.Ordinal))
                {
                    item.Path = detected.Directory;
                    updated++;
                }
            }
        }

        if (updated > 0)
        {
            SaveConfig();
            ReloadProfiles();
            await RefreshInstalledGamesAsync();
        }
    }


    [RelayCommand]
    private async Task RefreshInstalledGamesAsync()
    {
        IsCalculatingInstalledGames = true;
        try
        {
            var items = await Task.Run(() => Profiles.Where(x => x.IsInstalled)
                .Select(profile =>
                {
                    var path = ProfileConfig.ExpandPath(profile.Profile.Path);
                    long bytes = 0;
                    var files = new List<(string Key, long Length)>();
                    if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    {
                        try
                        {
                            foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
                            {
                                bytes += file.Length;
                                files.Add(($"{file.Length}/{file.Name}", file.Length));
                            }
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                    }

                    return (profile, bytes, files);
                })
                .ToList()).ConfigureAwait(true);

            InstalledGameItems.Clear();
            long total = 0;
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (profile, bytes, files) in items)
            {
                total += bytes;
                var server = GameChoices.SelectMany(x => x.Servers)
                    .FirstOrDefault(x => string.Equals(x.ProfileId, profile.Id, StringComparison.Ordinal)
                                         && x.Channel == ChannelOf(profile))
                    ?? GameChoices.SelectMany(x => x.Servers)
                        .FirstOrDefault(x => string.Equals(x.ProfileId, profile.Id, StringComparison.Ordinal));
                var card = GameChoices.FirstOrDefault(x => server is not null && x.Servers.Contains(server));
                InstalledGameItems.Add(new InstalledGameItem(
                    server?.GameName ?? profile.DisplayName,
                    server?.Name ?? "",
                    server?.BadgeText ?? "",
                    server?.BadgeBrush ?? Brushes.Transparent,
                    card?.Icon ?? server?.Icon,
                    FormatSize(bytes),
                    ProfileConfig.ExpandPath(profile.Profile.Path)));

                foreach (var (key, _) in files)
                {
                    seen[key] = seen.TryGetValue(key, out int count) ? count + 1 : 1;
                }
            }

            // Starward 用文件 ID 精确统计硬链接节省量；Linux 侧没有等价的托管 API，
            // 这里用「同名同大小出现在多个安装里」估算重复占用（文档已注明是估算）。
            long duplicated = 0;
            foreach (var (profile, _, files) in items)
            {
                foreach (var (key, length) in files)
                {
                    if (seen.TryGetValue(key, out int count) && count > 1)
                    {
                        duplicated += length;
                    }
                }
            }

            if (duplicated > 0)
            {
                InstalledGamesSizeText = FormatSize(total - duplicated);
                InstalledGamesSavedSizeText = FormatSize(duplicated);
            }
            else
            {
                InstalledGamesSizeText = FormatSize(total);
                InstalledGamesSavedSizeText = null;
            }
        }
        finally
        {
            IsCalculatingInstalledGames = false;
        }
    }


    /// <summary>磁盘占用（Starward 侧同样用两位小数的 GB / MB）。</summary>
    private static string FormatSize(long bytes)
    {
        const double Kb = 1024d;
        double gb = bytes / Kb / Kb / Kb;
        return gb >= 1 ? $"{gb:F2} GB" : $"{bytes / Kb / Kb:F0} MB";
    }


    [RelayCommand]
    private void OpenGameSelector()
    {
        BuildGameChoices();
        IsGameSelectorOpen = true;
        ShowGameIcons();
        _ = LoadChoiceArtAsync();
        _ = RefreshInstalledGamesAsync();
    }


    [RelayCommand]
    private void CloseGameSelector()
    {
        IsGameSelectorOpen = false;
        HideGameIcons();
    }


    /// <summary>
    /// 点卡片：只有一档区服时直接切过去并返回 true；多区服返回 false，由视图弹 flyout
    /// （Starward <c>Grid_GameBizDisplay_Tapped</c>，单选也弹；LunarWard 按验收要求单区服直切）。
    /// </summary>
    public bool ChooseGame(GameChoiceViewModel choice)
    {
        if (!choice.IsSingleServer)
        {
            return false;
        }

        ApplyServer(choice.Servers[0]);
        return true;
    }


    /// <summary>按 id 选中档案（自检 <c>--profile</c> 与 URL 协议共用）。</summary>
    public void SelectProfileById(string id)
    {
        if (Profiles.FirstOrDefault(x => x.Id == id) is { } item)
        {
            SelectProfile(item);
        }
    }


    private void SelectProfile(ProfileItemViewModel item)
    {
        if (ReferenceEquals(Profile, item))
        {
            return;
        }

        Profile = item;
        _services.Config.Launcher.LastProfileIndex = Profiles.IndexOf(item);
        SaveConfig();

        MarkCurrentServer();
        var generation = ++_artGeneration;
        _ = LoadArtAsync(generation);
        RaiseGameChanged(generation);
        RebuildPage();
    }


    /// <summary>当前档案 + 渠道（由 <c>Profile.Profile.Channel</c> 解析）。</summary>
    public Games.GameChannel CurrentChannel => ChannelOf(Profile);


    /// <summary>全部卡片上的全部区服（每档区服同时出现在卡片和图标条里）。</summary>
    public IEnumerable<GameServerItem> AllServers => GameChoices.SelectMany(x => x.Servers);


    private static Games.GameChannel ChannelOf(ProfileItemViewModel? profile)
        => string.Equals(profile?.Profile.Channel, nameof(Games.GameChannel.Bilibili), StringComparison.OrdinalIgnoreCase)
            ? Games.GameChannel.Bilibili
            : Games.GameChannel.Official;


    /// <summary>把所有卡片 / 图标条条目的「当前」标记重算一遍。</summary>
    private void MarkCurrentServer()
    {
        var channel = CurrentChannel;
        foreach (var server in GameChoices.SelectMany(x => x.Servers))
        {
            server.IsCurrent = string.Equals(server.ProfileId, Profile?.Id, StringComparison.Ordinal)
                               && server.Channel == channel;
        }

        foreach (var choice in GameChoices)
        {
            choice.IsCurrent = choice.Servers.Any(x => x.IsCurrent);
        }

        RefreshCurrentServer();
    }


    private void RefreshCurrentServer()
    {
        CurrentServer = GameChoices.SelectMany(x => x.Servers)
            .FirstOrDefault(x => string.Equals(x.ProfileId, Profile?.Id, StringComparison.Ordinal)
                                 && x.Channel == CurrentChannel);
    }


    /// <summary>按配置顺序重建左上角图标条（Starward <c>GameBizIcons</c>）。</summary>
    private void UpdatePinnedServers()
    {
        var order = _services.Config.Launcher.PinnedGameServers;
        var pinned = GameChoices.SelectMany(x => x.Servers)
            .Where(x => x.IsPinned)
            .OrderBy(x =>
            {
                int index = order.IndexOf(x.Key);
                return index < 0 ? int.MaxValue : index;
            })
            .ToList();

        PinnedServers.Clear();
        foreach (var server in pinned)
        {
            PinnedServers.Add(server);
        }
    }


    // ── 图标与背景 ────────────────────────────────────────────────────

    [ObservableProperty]
    private Bitmap? _currentGameIcon;

    public bool HasGameIcon => CurrentGameIcon is not null;

    partial void OnCurrentGameIconChanged(Bitmap? value) => OnPropertyChanged(nameof(HasGameIcon));


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackgroundImage))]
    private Bitmap? _backgroundImage;

    public bool HasBackgroundImage => BackgroundImage is not null;


    [ObservableProperty]
    private bool _isBackgroundLoading;


    /// <summary>当前游戏的 biz（<c>hk4e_cn</c> 等），供素材/公告/便笺使用。</summary>
    public string CurrentGameBiz => Games.GameCatalog.Get(
        Profile.Profile.Game,
        string.Equals(Profile.Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
            ? Games.GameEdition.Global
            : Games.GameEdition.China)?.GameBiz ?? "hk4e_cn";


    /// <summary>某个档案对应的 launcher id（国服官方 / 国际服官方 / B 站）。</summary>
    public static string LauncherIdFor(ProfileItemViewModel profile)
    {
        var biz = Games.GameCatalog.Get(
            profile.Profile.Game,
            string.Equals(profile.Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
                ? Games.GameEdition.Global
                : Games.GameEdition.China)?.GameBiz ?? "hk4e_cn";
        return Core.HoYoPlay.LauncherId.FromGameBiz(new Core.GameBiz(biz)) ?? Core.HoYoPlay.LauncherId.ChinaOfficial;
    }


    /// <summary>当前游戏的 launcher id（官方/ B 站）。</summary>
    public string CurrentLauncherId => Core.HoYoPlay.LauncherId.FromGameBiz(new Core.GameBiz(CurrentGameBiz))
                                       ?? Core.HoYoPlay.LauncherId.ChinaOfficial;


    // ── 生命周期 ──────────────────────────────────────────────────────

    private async Task InitializeAsync()
    {
        // 背景先用本地自定义图，再用官方素材覆盖（Starward 也是本地优先）
        ShowLocalBackground();
        await LoadArtAsync(++_artGeneration).ConfigureAwait(true);
    }


    private async Task LoadArtAsync(int generation)
    {
        try
        {
            IsBackgroundLoading = true;
            await Assets.LoadAsync(CurrentLauncherId, CurrentGameBiz).ConfigureAwait(true);
            var art = Assets.For();

            if (await Assets.BitmapAsync(art?.Icon).ConfigureAwait(true) is { } icon)
            {
                CurrentGameIcon = icon;
            }

            if (_videoBackground is null && await Assets.BitmapAsync(art?.Background).ConfigureAwait(true) is { } background)
            {
                BackgroundImage = background;
            }

            // 游戏选择层要看到全部游戏的卡片：先把各 launcher 的 getGames 拉齐，
            // 再只下载缩略图/Logo（大背景图只给当前游戏）。
            await Assets.LoadAllLaunchersAsync(Profiles.Select(LauncherIdFor).Distinct()).ConfigureAwait(true);
            await LoadChoiceArtAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Warn($"素材加载失败：{ex.Message}");
        }
        finally
        {
            IsBackgroundLoading = false;

            // 切换时已经立即触发过一次就不再补发：否则外壳会把页面重建两遍（背景闪一下、视频重启）。
            // 启动时没有立即触发，这里仍然补发一次，让图标条/素材相关绑定刷新。
            if (generation == _artGeneration && generation != _lastGameChangedGeneration)
            {
                RaiseGameChanged(generation);
            }
        }
    }


    /// <summary>本地背景（<c>~/.cache/lunarward/background</c>），存在时优先于官方图。</summary>
    private void ShowLocalBackground()
    {
        try
        {
            var items = _services.Background.Discover();
            var video = items.FirstOrDefault(x => x.IsVideo);
            var image = items.FirstOrDefault(x => x.ImagePath is not null);
            if (video?.VideoPath is { } videoPath
                && _services.Background.IsVideoAvailable
                && _services.Config.Launcher.VideoBackground)
            {
                ShowVideoBackground(videoPath);
                return;
            }

            if (image is not null)
            {
                BackgroundImage = new Bitmap(image.ImagePath!);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"本地背景加载失败：{ex.Message}");
        }
    }


    private string? _videoBackground;

    /// <summary>视图收到后把 VideoView 挂到背景层。</summary>
    public event EventHandler<string>? VideoBackgroundRequested;

    /// <summary>切回静态图时通知视图卸载 VideoView（否则残留黑块）。</summary>
    public event EventHandler? VideoBackgroundCleared;

    public string? VideoBackgroundPath => _videoBackground;


    /// <summary>
    /// 切到视频背景：本地文件或官方 https 视频（ffmpeg 子进程流式解码）。
    /// </summary>
    public void ShowVideoBackground(string source, string? overlaySource = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        _videoBackground = source;
        BackgroundImage = null;
        _ = SetBackgroundOverlayAsync(overlaySource);
        VideoBackgroundRequested?.Invoke(this, source);
    }


    /// <summary>切到静态图背景（本地或远程），同时卸载视频层。</summary>
    public async Task ShowStaticBackgroundAsync(string? source, string? overlaySource = null)
    {
        ClearVideoBackground();
        await SetBackgroundOverlayAsync(overlaySource).ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        if (await Assets.BitmapAsync(source).ConfigureAwait(true) is { } bitmap)
        {
            BackgroundImage = bitmap;
        }
    }


    private Bitmap? _backgroundOverlay;

    /// <summary>
    /// 版本亮点海报（上游 <c>gameBackground.Theme</c>：透明底 PNG，左上角是 logo 与版本标题文字），
    /// 叠在背景图或视频帧之上。本地自定义背景没有这一层。
    /// </summary>
    public Bitmap? BackgroundOverlayImage
    {
        get => _backgroundOverlay;
        private set
        {
            if (SetProperty(ref _backgroundOverlay, value))
            {
                OnPropertyChanged(nameof(HasBackgroundOverlay));
            }
        }
    }

    public bool HasBackgroundOverlay => _backgroundOverlay is not null;


    /// <summary>换/清版本亮点海报；空值表示该背景没有 theme（本地自定义背景）。</summary>
    public async Task SetBackgroundOverlayAsync(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            BackgroundOverlayImage = null;
            return;
        }

        try
        {
            BackgroundOverlayImage = await Assets.BitmapAsync(source).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Warn($"版本亮点海报加载失败：{ex.Message}");
            BackgroundOverlayImage = null;
        }
    }


    /// <summary>卸载视频背景（不改变当前静态图）。</summary>
    public void ClearVideoBackground()
    {
        if (_videoBackground is null)
        {
            return;
        }

        _videoBackground = null;
        VideoBackgroundCleared?.Invoke(this, EventArgs.Empty);
    }


    // ── 档案 ──────────────────────────────────────────────────────────

    public void ReloadProfiles()
    {
        var previousId = Profile?.Id;
        Profiles.Clear();

        // Starward 的网格选择器列出所有游戏；已安装的给出 profile
        foreach (var definition in Games.GameCatalog.All)
        {
            var existing = _services.Config.Profiles.FirstOrDefault(kv =>
                kv.Value.Game == definition.Kind
                && string.Equals(kv.Value.Edition, definition.Edition == Games.GameEdition.Global ? "Global" : "China",
                    StringComparison.OrdinalIgnoreCase));

            var isInstalled = existing.Key is not null && Directory.Exists(ProfileConfig.ExpandPath(existing.Value.Path));
            Profiles.Add(new ProfileItemViewModel(
                existing.Key ?? $"{definition.Kind}-{(definition.Edition == Games.GameEdition.Global ? "global" : "cn")}",
                existing.Value ?? new ProfileConfig { Game = definition.Kind, Path = "", Edition = definition.Edition == Games.GameEdition.Global ? "Global" : "China", Channel = "Official" },
                definition.DisplayName)
            {
                IsInstalled = isInstalled,
            });
        }

        EnsureDefaultRuntime();

        var index = Math.Clamp(_services.Config.Launcher.LastProfileIndex, 0, Math.Max(0, Profiles.Count - 1));
        Profile = Profiles.FirstOrDefault(p => p.Id == previousId) ?? Profiles[index];

        if (!ReferenceEquals(Profile, null))
        {
            _services.Config.Launcher.LastProfileIndex = Profiles.IndexOf(Profile);
        }

        // 左上角图标条与全屏选择层共用 GameChoices：档案变化后重建，并标出当前游戏。
        BuildGameChoices();
        MarkCurrentServer();
        if (IsGameIconsPinned)
        {
            IsGameIconsVisible = true;
        }
    }


    /// <summary>
    /// 重建卡片（每款游戏一张，国服/国际服/B 站在卡片内）与图标条。
    /// 语言过滤、区服归组都在 <see cref="GameChoiceBuilder"/> 里，便于单测。
    /// </summary>
    private void BuildGameChoices()
    {
        GameChoices.Clear();
        foreach (var choice in GameChoiceBuilder.Build(
                     Profiles,
                     Strings.Language,
                     _services.Config.Launcher.PinnedGameServers,
                     _services.Config.Launcher.GameChoiceOrder,
                     Profile?.Id,
                     CurrentChannel))
        {
            GameChoices.Add(choice);
        }

        UpdatePinnedServers();
        RefreshCurrentServer();
        _ = LoadChoiceArtAsync();
    }


    /// <summary>游戏选择层的缩略图/Logo（Starward GameSelector 的 192×108 卡片）。</summary>
    private async Task LoadChoiceArtAsync()
    {
        try
        {
            IsGameSelectorLoading = true;
            await Assets.LoadAllLaunchersAsync(Profiles.Select(LauncherIdFor).Distinct()).ConfigureAwait(true);
            foreach (var choice in GameChoices)
            {
                // 图标条按区服的 gamebiz 取 40×40 方形图标（国服/国际服图标不同）。
                foreach (var server in choice.Servers)
                {
                    if (await Assets.BitmapAsync(Assets.For(server.GameBiz)?.Icon).ConfigureAwait(true) is { } serverIcon)
                    {
                        server.Icon = serverIcon;
                    }
                }

                // 全屏卡片用 192×108 缩略图 + Logo。
                if (await Assets.BitmapAsync(Assets.For(choice.GameBiz)?.Icon).ConfigureAwait(true) is { } icon)
                {
                    choice.Icon = icon;
                }

                if (await Assets.CardArtAsync(choice.GameBiz).ConfigureAwait(true) is not { } art)
                {
                    continue;
                }

                choice.Thumbnail = art.Thumbnail;
                choice.Logo = art.Logo;
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"游戏选择层素材加载失败：{ex.Message}");
        }
        finally
        {
            IsGameSelectorLoading = false;
        }
    }


    private void EnsureDefaultRuntime()
    {
        if (!string.IsNullOrEmpty(_services.Config.Launcher.DefaultRuntime)
            && _services.Config.Runtimes.ContainsKey(_services.Config.Launcher.DefaultRuntime))
        {
            return;
        }

        var available = new RuntimeScanner(_services.Paths).Scan();
        var picked = available.FirstOrDefault(x => x.Source == "System")
                     ?? available.FirstOrDefault(x => x.Source == "Managed")
                     ?? available.FirstOrDefault(x => x.Source == "Borrowed")
                     ?? available.FirstOrDefault(x => x.Source == "Proton");
        if (picked is null)
        {
            return;
        }

        _services.Config.Runtimes[picked.Name] = new RuntimeConfigDto
        {
            Runner = RunnerDto.FromDomain(picked.Runner),
            Dxvk = new DxvkDto { Kind = "System" },
        };
        _services.Config.Launcher.DefaultRuntime = picked.Name;
        foreach (var profile in _services.Config.Profiles.Values)
        {
            profile.Runtime = new ScopedRuntimeDto { Kind = "Default" };
        }

        SaveConfig();
    }


    private void SaveConfig()
    {
        try
        {
            _services.Config.Save(_services.Paths.ConfigFile);
        }
        catch (Exception ex)
        {
            _log.Error("保存配置失败", ex);
        }
    }


    public void Shutdown() => _services.LauncherService.Shutdown();


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Assets.Dispose();
    }
}
