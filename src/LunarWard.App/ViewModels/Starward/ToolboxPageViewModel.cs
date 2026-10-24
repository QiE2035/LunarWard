using System.Collections.ObjectModel;
using System.Net.Http;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

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
/// 战绩子页（深境螺旋 / 式舆防卫战…）尚未移植：点菜单项只显示"尚未移植"的占位，
/// 不伪造数据；未选中时与 Starward 一样是空白 Frame。
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
        _gameKind = Profile.Profile.Game;
        _gameBiz = Shell.CurrentGameBiz;
        _server = _gameBiz.EndsWith("_global", StringComparison.Ordinal) ? "global" : "cn";

        BuildMenuItems();
        if (Environment.GetEnvironmentVariable("LUNARWARD_TOOLBOX_DEMO") == "1")
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
        OnPropertyChanged(nameof(ShowNotPortedHint));
        OnPropertyChanged(nameof(PlaceholderTitle));
    }


    /// <summary>已移植的子页由视图层塞进 ContentControl（视图侧置 true）。</summary>
    [ObservableProperty]
    private bool _hasHostedPage;

    partial void OnHasHostedPageChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(ShowNotPortedHint));
    }

    /// <summary>选中战绩页 → 显示"尚未移植"占位；未选中时 Starward 是空白 Frame。</summary>
    public bool ShowPlaceholder => SelectedItem is not null && !HasHostedPage;

    /// <summary>占位提示只对尚未移植的页显示（深境螺旋 / 幻想真境剧诗 / 幽境危战 / 绳网月报已移植）。</summary>
    public bool ShowNotPortedHint => ShowPlaceholder && !PortedTags.Contains(SelectedItem?.Tag ?? "");

    /// <summary>已经 1:1 移植、能真正挂载子页的战绩菜单。</summary>
    public static IReadOnlySet<string> PortedTags { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "SpiralAbyss",
        "ImaginariumTheater",
        "StygianOnslaught",
        "InterKnotMonthlyReport",
    };

    public string PlaceholderTitle => SelectedItem?.Title ?? "";


    [RelayCommand]
    private void SelectMenu(ToolboxMenuItem? item) => SelectedItem = item;


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


    private void ApplyRole(GameRecordRole role)
    {
        CurrentRole = role;
        HasRole = true;
        Nickname = role.Nickname ?? "";
        RegionText = string.IsNullOrWhiteSpace(role.RegionName) ? role.Region : role.RegionName;
        LevelText = role.Level > 0 ? $"Lv.{role.Level}" : "";
        StatusText = "";
        ErrorText = null;

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


    /// <summary>「更新设备指纹」在 Starward 用于战绩 API 风控；LunarWard 暂未接，保留位置但禁用。</summary>
    public bool CanUpdateDeviceFingerprint => false;


    // ── 本机游戏账号（LunarWard 追加） ────────────────────────────────

    /// <summary>请求页面打开 wine 账号快照窗口（视图层负责 new Window）。</summary>
    public event EventHandler? LocalAccountsRequested;

    [RelayCommand]
    private void OpenLocalAccounts() => LocalAccountsRequested?.Invoke(this, EventArgs.Empty);


    private void BuildMenuItems()
    {
        MenuItems.Clear();

        if (string.Equals(_gameKind, "genshin", StringComparison.OrdinalIgnoreCase))
        {
            Add("BattleChronicle", "\uE9D9", "Toolbox.BattleChronicle");
            Add("TravelersDiary", "\uE787", "Toolbox.TravelersDiary");
            Add("SpiralAbyss", "\uE8A1", "Toolbox.SpiralAbyss");
            Add("ImaginariumTheater", "\uE7FC", "Toolbox.ImaginariumTheater");
            Add("StygianOnslaught", "\uE9D2", "Toolbox.StygianOnslaught");
        }
        else
        {
            Add("InterKnotMonthlyReport", "\uE9D2", "Toolbox.InterKnotMonthly");
            Add("ShiyuDefense", "\uE8A1", "Toolbox.ShiyuDefense");
            Add("DeadlyAssault", "\uE7FC", "Toolbox.DeadlyAssault");
        }
    }


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
