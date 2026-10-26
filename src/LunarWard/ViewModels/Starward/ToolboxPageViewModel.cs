using LunarWard.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Launch;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 工具箱页：复刻 Starward <c>Features/GameRecord/GameRecordPage.xaml</c> 的容器。
/// </summary>
/// <remarks>
/// <para>
/// Starward 的工具箱是 <c>NavigationView</c>：248px 左栏（头像 / 登录 / 角色列表 +
/// 战绩菜单 + 底部「更新设备指纹」）+ 右侧 <c>Frame</c>。LunarWard 没有 SQLite 角色库，
/// 凭据复用便笺的 <see cref="DailyNoteService"/>（同一份米游社 / HoYoLAB cookie），
/// 角色通过 <see cref="GameRecordClient.GetGameRolesAsync"/> 实时拉取。
/// </para>
/// <para>
/// 战绩子页（深境螺旋 / 幻想真境剧诗 / 幽境危战 / 绳网月报 / 危局强袭战）已移植，
/// 战绩菜单里的 14 个子页全部已移植（5 个原神 + 9 个崩铁/绝区零）；
/// 未选中时与 Starward 一样是空白 Frame。
/// </para>
/// <para>
/// LunarWard 追加：左栏底部「本机游戏账号」打开既有的 wine 账号快照窗口
/// （Starward 对应能力是启动器的 GameAccountSwitcher，默认隐藏）。
/// </para>
/// </remarks>
public sealed partial class ToolboxPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly DailyNoteService _credentials;

    private readonly string _gameKind;

    private readonly string _gameBiz;

    private readonly string _server;

    private string _cookie = "";


    public ToolboxPageViewModel(PageContext context) : base(context)
    {
        _credentials = new DailyNoteService(context.Services.Paths);
        // 自检/截图可用 LUNARWARD_TOOLBOX_GAME 覆盖（例如 --tab 切到崩铁菜单）；正常启动仍读当前配置。
        _gameKind = Environment.GetEnvironmentVariable("LUNARWARD_TOOLBOX_GAME") ?? Profile.Profile.Game;
        _gameBiz = Shell.CurrentGameBiz;
        _server = _gameBiz.EndsWith("_global", StringComparison.Ordinal) ? "global" : "cn";

        BuildMenuItems();
        if (ToolboxDemo.Enabled)
        {
            // 自检/截图夹具：不联网、不需要真实 cookie（与 Starward x:DesignInstance 同用途）。
            ApplyDemoRole();
        }
        else
        {
            _ = LoadRoleAsync();
        }
    }


    private void ApplyDemoRole()
    {
        var role = new GameRecordRole
        {
            Uid = 100000000,
            GameBiz = _gameBiz,
            Nickname = "LunarWard",
            Level = 60,
            Region = "cn_gf01",
            RegionName = "天空岛",
        };
        ApplyRole(role);
        HasHostedPage = false;
    }


    /// <summary>左栏战绩菜单（Starward 的 NavigationView.MenuItems，按游戏显示）。</summary>
    public ObservableCollection<ToolboxMenuItem> MenuItems { get; } = [];


    [ObservableProperty]
    private ToolboxMenuItem? _selectedItem;


    partial void OnSelectedItemChanged(ToolboxMenuItem? value)
    {
        foreach (var item in MenuItems)
        {
            item.IsSelected = ReferenceEquals(item, value);
        }

        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(ShowWebRecordHint));
        OnPropertyChanged(nameof(PlaceholderTitle));
    }


    /// <summary>已移植的子页由视图层塞进 ContentControl（视图侧置 true）。</summary>
    [ObservableProperty]
    private bool _hasHostedPage;

    partial void OnHasHostedPageChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(ShowWebRecordHint));
    }

    /// <summary>选中战绩页 → 显示"尚未移植"占位；未选中时 Starward 是空白 Frame。</summary>
    public bool ShowPlaceholder => SelectedItem is not null && !HasHostedPage;

    /// <summary>「战绩」在 Starward 是独立 WebView 窗口，Linux 下等价为在系统浏览器打开同名网页。</summary>
    public bool ShowWebRecordHint => ShowPlaceholder && SelectedItem?.Tag is "BattleChronicle";

    public string PlaceholderTitle => SelectedItem?.Title ?? "";


    [RelayCommand]
    private void SelectMenu(ToolboxMenuItem? item)
    {
        SelectedItem = item;
        if (item?.Tag is "BattleChronicle" && !ToolboxDemo.Enabled)
        {
            _ = OpenBattleChronicleAsync();
        }
    }


    // ── 角色 / 登录 ──────────────────────────────────────────────────

    [ObservableProperty]
    private bool _hasRole;

    /// <summary>当前角色（深境螺旋等战绩子页需要；Starward 从 SQLite 读，这里从接口/便笺 cookie 来）。</summary>
    public GameRecordRole? CurrentRole { get; private set; }

    [ObservableProperty]
    private string _nickname = "";

    [ObservableProperty]
    private string _regionText = "";

    [ObservableProperty]
    private string _levelText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    private Bitmap? _avatar;

    public bool HasAvatar => Avatar is not null;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private string _statusText = "";

    /// <summary>登录 Flyout 的 cookie 输入框。</summary>
    [ObservableProperty]
    private string _cookieInput = "";

    /// <summary>角色列表 Flyout（Starward <c>Flyout_GameRoles</c>）里展示的已保存角色。</summary>
    public ObservableCollection<ToolboxRoleItem> Roles { get; } = [];


    /// <summary>页面重新进入时：还没读到角色就再读一次（便笺 Flyout 可能刚保存了 cookie）。</summary>
    public void ReloadRoleIfNeeded()
    {
        if (!HasRole)
        {
            _ = LoadRoleAsync();
        }
    }


    private async Task LoadRoleAsync()
    {
        var saved = _credentials.GetCredentials(_server);
        if (saved is null || string.IsNullOrWhiteSpace(saved.Cookie))
        {
            return;
        }

        _cookie = saved.Cookie;
        IsBusy = true;
        try
        {
            var role = await FetchRoleAsync(_cookie).ConfigureAwait(true);
            if (role is not null)
            {
                ApplyRole(role);
            }
            else
            {
                ErrorText = Strings["Toolbox.CookieInvalid"];
            }
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["Toolbox.LoadRoleFailed"], ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }


    private async Task<GameRecordRole?> FetchRoleAsync(string cookie)
    {
        GameRecordClient client = _server == "global"
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);

        var roles = await client.GetGameRolesAsync(cookie, new GameBiz(_gameBiz)).ConfigureAwait(false);
        return roles.FirstOrDefault();
    }


    private async Task LoadAvatarAsync(string url)
    {
        try
        {
            var bitmap = await GachaIconCache.LoadAsync(url).ConfigureAwait(false);
            if (bitmap is not null)
            {
                GachaIconCache.Post(() => Avatar = bitmap);
            }
        }
        catch (Exception)
        {
            // 头像失败不影响页面（保留占位图标）
        }
    }


    private void ApplyRole(GameRecordRole role)
    {
        CurrentRole = role;
        HasRole = true;
        Nickname = role.Nickname ?? "";
        RegionText = string.IsNullOrWhiteSpace(role.RegionName) ? role.Region : role.RegionName;
        LevelText = role.Level > 0 ? $"Lv.{role.Level}" : "";
        StatusText = "";
        ErrorText = null;

        // 头像：上游 GetGameRolesAsync 已经把 role.HeadIcon 填好，这里把 URL 变成位图。
        // 复用 GachaIconCache 的「内存 + 磁盘」缓存（同一个 HttpClient，目录名沿用
        // gacha-icons 仅为兼容已有缓存，头像 URL 自带唯一后缀，不会撞名）。
        Avatar = GachaIconCache.TryGet(role.HeadIcon);
        if (Avatar is null && !string.IsNullOrWhiteSpace(role.HeadIcon))
        {
            _ = LoadAvatarAsync(role.HeadIcon);
        }

        Roles.Clear();
        Roles.Add(new ToolboxRoleItem(role, _cookie, _server));
    }


    /// <summary>登录 Flyout 的「保存」：校验 cookie、拉角色、落盘。</summary>
    [RelayCommand]
    private async Task SaveCookieAsync()
    {
        var cookie = CookieInput.Trim();
        if (string.IsNullOrWhiteSpace(cookie))
        {
            ErrorText = Strings["Toolbox.CookieRequired"];
            return;
        }

        IsBusy = true;
        ErrorText = null;
        try
        {
            var role = await FetchRoleAsync(cookie).ConfigureAwait(true);
            if (role is null)
            {
                ErrorText = Strings["Toolbox.CookieInvalid"];
                return;
            }

            _cookie = cookie;
            _credentials.SaveCredentials(_server, cookie, role.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture), role.Region);
            ApplyRole(role);
            StatusText = Strings["Toolbox.CookieSaved"];
            CookieInput = "";
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["Toolbox.LoadRoleFailed"], ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }


    /// <summary>角色列表里的「刷新信息」。</summary>
    [RelayCommand]
    private async Task RefreshRoleAsync()
    {
        if (string.IsNullOrWhiteSpace(_cookie))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var role = await FetchRoleAsync(_cookie).ConfigureAwait(true);
            if (role is not null)
            {
                ApplyRole(role);
            }
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["Toolbox.LoadRoleFailed"], ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }


    /// <summary>角色列表里的「删除角色」：清掉本机保存的 cookie。</summary>
    [RelayCommand]
    private void DeleteRole(ToolboxRoleItem? item)
    {
        if (item is null)
        {
            return;
        }

        _credentials.ClearCredentials(item.Server);
        _cookie = "";
        CurrentRole = null;
        HasRole = false;
        Nickname = "";
        RegionText = "";
        LevelText = "";
        Avatar = null;
        Roles.Clear();
        StatusText = "";
    }


    /// <summary>
    /// 「更新设备指纹」是否可用：与上游 <c>GameRecordService.UpdateDeviceFpAsync</c> 一致，
    /// 只有国服（<c>HyperionClient</c>）有这条链路，HoYoLAB 直接返回。
    /// </summary>
    public bool CanUpdateDeviceFingerprint => _server == "cn";


    /// <summary>
    /// 更新设备指纹（对应上游 <c>GameRecordPage.xaml.cs</c> 的 <c>UpdateDeviceInfoAsync(true)</c>）：
    /// 带上已保存的 device_id/fp 拉新指纹，成功后落盘 <see cref="Launch.ToolboxSettings"/>。
    /// </summary>
    [RelayCommand]
    private async Task UpdateDeviceFingerprintAsync()
    {
        if (!CanUpdateDeviceFingerprint)
        {
            return;
        }

        IsBusy = true;
        ErrorText = null;
        StatusText = Strings["Toolbox.FingerprintUpdating"];
        try
        {
            var client = new HyperionClient(Http);
            var toolbox = Services.Config.Toolbox;
            if (!string.IsNullOrWhiteSpace(toolbox.DeviceId) && !string.IsNullOrWhiteSpace(toolbox.DeviceFp))
            {
                client.DeviceId = toolbox.DeviceId;
                client.DeviceFp = toolbox.DeviceFp;
            }

            var fp = await client.GetDeviceFpAsync().ConfigureAwait(true);
            toolbox.DeviceId = client.DeviceId;
            toolbox.DeviceFp = string.IsNullOrWhiteSpace(fp) ? client.DeviceFp : fp;
            toolbox.DeviceFpLastUpdate = DateTimeOffset.Now;
            Services.Config.Save(Services.Paths.ConfigFile);
            StatusText = Strings["Toolbox.FingerprintUpdated"];
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Strings["Toolbox.FingerprintFailed"], ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }


    // ── 本机游戏账号（LunarWard 追加） ────────────────────────────────

    /// <summary>请求页面打开 wine 账号快照窗口（视图层负责 new Window）。</summary>
    public event EventHandler? LocalAccountsRequested;

    [RelayCommand]
    private void OpenLocalAccounts() => LocalAccountsRequested?.Invoke(this, EventArgs.Empty);


    /// <summary>
    /// 战绩网页地址，照抄 Starward <c>BBSWebBridge.xaml.cs</c> 的 <c>(GlobalServer, Game)</c> 映射。
    /// Starward 把网页塞进 WebView2 窗口并注入米游社登录 cookie；Linux 上改为系统浏览器打开。
    /// </summary>
    private string? BattleChronicleUrl => (_server == "global", _gameKind) switch
    {
        (true, "genshin") => "https://act.hoyolab.com/app/community-game-records-sea/m.html?gid=2",
        (true, "starrail") => "https://act.hoyolab.com/app/community-game-records-sea/m.html?gid=6",
        (true, "zzz") => "https://act.hoyolab.com/app/zzz-game-record/m.html?gid=8",
        (false, "genshin") => "https://webstatic.mihoyo.com/app/community-game-records/?game_id=2",
        (false, "starrail") => "https://webstatic.mihoyo.com/app/community-game-records/?game_id=6",
        (false, "zzz") => "https://act.mihoyo.com/app/mihoyo-zzz-game-record/m.html?game_id=8",
        _ => null,
    };


    private async Task OpenBattleChronicleAsync()
    {
        if (BattleChronicleUrl is null)
        {
            return;
        }

        try
        {
            await Services.OpenUriAsync(BattleChronicleUrl).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    private void BuildMenuItems()
    {
        MenuItems.Clear();

        var entries = GetMenuEntries(_gameKind);
        foreach (var (tag, icon, titleKey) in entries)
        {
            Add(tag, icon, titleKey);
        }

        if (entries.Count == 0 && !_unknownGameWarned)
        {
            _unknownGameWarned = true;
            Log.Warn($"工具箱：未知游戏 \"{_gameKind}\"，战绩菜单留空。");
        }
    }


    private bool _unknownGameWarned;


    /// <summary>
    /// 战绩菜单的（Tag, 图标, 文案索引）映射，按游戏分组。
    /// 可见性规则照抄 Starward <c>Features/GameRecord/GameRecordPage.xaml.cs</c> 的
    /// <c>InitializeNavigationViewItemVisibility</c>：hk4e / hkrpg / nap 各自只显示本游戏
    /// 拥有的子页；未知游戏返回空集合（不猜，交给调用方告警）。
    /// </summary>
    /// <remarks>public 以便单测直接断言三个游戏的 Tag 集合与顺序。</remarks>
    public static IReadOnlyList<(string Tag, string Icon, string TitleKey)> GetMenuEntries(string? gameKind)
        => gameKind?.ToLowerInvariant() switch
        {
            // 原神：战绩、旅行者札记、深境螺旋、幻想真境剧诗、幽境危战
            "genshin" =>
            [
                ("BattleChronicle", "\uE9D9", "Toolbox.BattleChronicle"),
                ("TravelersDiary", "\uE787", "Toolbox.TravelersDiary"),
                ("SpiralAbyss", "\uE8A1", "Toolbox.SpiralAbyss"),
                ("ImaginariumTheater", "\uE7FC", "Toolbox.ImaginariumTheater"),
                ("StygianOnslaught", "\uE9D2", "Toolbox.StygianOnslaught"),
            ],
            // 崩坏：星穹铁道（菜单里没有战绩项，沿用本仓库既有组合）
            "starrail" =>
            [
                ("TrailblazeCalendar", "\uE787", "Toolbox.TrailblazeCalendar"),
                ("SimulatedUniverse", "\uE9D9", "Toolbox.SimulatedUniverse"),
                ("ForgottenHall", "\uE8A1", "Toolbox.ForgottenHall"),
                ("PureFiction", "\uE7C1", "Toolbox.PureFiction"),
                ("ApocalypticShadow", "\uE7C3", "Toolbox.ApocalypticShadow"),
                ("ChallengePeak", "\uE9D9", "Toolbox.ChallengePeak"),
            ],
            // 绝区零：战绩、绳网月报、式舆防卫战、危局强袭战
            "zzz" =>
            [
                ("BattleChronicle", "\uE9D9", "Toolbox.BattleChronicle"),
                ("InterKnotMonthlyReport", "\uE9D2", "Toolbox.InterKnotMonthly"),
                ("ShiyuDefense", "\uE8A1", "Toolbox.ShiyuDefense"),
                ("DeadlyAssault", "\uE7FC", "Toolbox.DeadlyAssault"),
            ],
            _ => [],
        };


    private void Add(string tag, string icon, string titleKey)
        => MenuItems.Add(new ToolboxMenuItem(tag, icon, Strings[titleKey]));


    public override void Dispose()
    {
        LocalAccountsRequested = null;
        base.Dispose();
    }
}


/// <summary>工具箱左栏的一条战绩菜单（Starward <c>NavigationViewItem</c>）。</summary>
public sealed partial class ToolboxMenuItem(string tag, string icon, string title) : ObservableObject
{
    public string Tag { get; } = tag;

    public string Icon { get; } = icon;

    public string Title { get; } = title;

    [ObservableProperty]
    private bool _isSelected;
}


/// <summary>角色列表 Flyout 的一行（Starward <c>ListView_GameRoles</c>）。</summary>
public sealed class ToolboxRoleItem
{
    public ToolboxRoleItem(GameRecordRole role, string cookie, string server)
    {
        Role = role;
        Cookie = cookie;
        Server = server;
    }

    public GameRecordRole Role { get; }

    public string Cookie { get; }

    public string Server { get; }

    public string Nickname => Role.Nickname ?? "";

    public string Detail => string.IsNullOrWhiteSpace(Role.RegionName)
        ? $"{Role.Region} Lv.{Role.Level}"
        : $"{Role.RegionName} Lv.{Role.Level}";

    /// <summary>XAML 文案索引器（DeleteRole 的 ToolTip）。</summary>
    public Localization.AppStrings Strings => Localization.AppStrings.Instance;
}
