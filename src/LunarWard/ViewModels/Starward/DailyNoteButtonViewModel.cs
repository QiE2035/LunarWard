using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Launch;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 启动器右下「每日便笺」按钮的 Flyout（复刻 Starward <c>DailyNoteButton.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 的便笺是按钮 + Flyout，不是独立页面：刷新按钮、角色行、分隔线、错误行，
/// 然后是游戏各自的分区（原神：树脂/委托/洞天宝钱/探索派遣；绝区零：电量/绳网会员/
/// 每日任务/周期任务）。LunarWard 只支持原神/绝区零，所以 BH3/星铁分区不出现
/// （Starward 也是按 <c>IsXxxEnabled</c> 隐藏）。
/// </para>
/// <para>
/// 数据来自 <see cref="DailyNoteService"/>（cookie 存在本机受限文件里）；没有 cookie 时
/// Flyout 给出「去工具箱保存 cookie」的入口，而不是假装有数据。
/// </para>
/// </remarks>
public sealed partial class DailyNoteButtonViewModel : ObservableObject
{
    private readonly PageContext _context;

    private readonly DailyNoteService _service;

    private readonly string _gameKind;

    private readonly string _gameBiz;

    private readonly string _server;

    private string _cookie = "";

    private string _uid = "";

    private string _region = "";


    public DailyNoteButtonViewModel(PageContext context)
    {
        _context = context;
        _service = new DailyNoteService(context.Services.Paths);
        _gameKind = context.Profile.Profile.Game;
        _gameBiz = context.Shell.CurrentGameBiz;
        _server = _gameBiz.EndsWith("_global", StringComparison.Ordinal) ? "global" : "cn";

        var saved = _service.GetCredentials(_server);
        _cookie = saved?.Cookie ?? "";
        _uid = saved?.Uid ?? "";
        _region = saved?.Region ?? (_server == "global" ? "os_asia" : "cn_gf01");

        HasCredentials = !string.IsNullOrWhiteSpace(_cookie);
        StatusText = HasCredentials ? "" : Strings["DailyNote.NoCookie"];
    }


    public Localization.AppStrings Strings => Localization.AppStrings.Instance;

    public bool IsGenshin => string.Equals(_gameKind, "genshin", StringComparison.OrdinalIgnoreCase);

    public bool IsZzz => string.Equals(_gameKind, "zzz", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private bool _hasCredentials;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _nickname = "";

    [ObservableProperty]
    private string _regionText = "";

    [ObservableProperty]
    private string _levelText = "";

    [ObservableProperty]
    private string _primaryName = "";

    [ObservableProperty]
    private int _primaryCurrent;

    [ObservableProperty]
    private int _primaryMax;

    [ObservableProperty]
    private double _primaryPercent;

    [ObservableProperty]
    private string _recoveryText = "";

    /// <summary>原神分区（Starward <c>Grid_Genshin</c>）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGenshin))]
    private GenshinDailyNoteExtra? _genshin;

    /// <summary>绝区零分区（Starward <c>Grid_ZZZ</c>）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowZzz))]
    private ZzzDailyNoteExtra? _zzz;

    /// <summary>原神分区只在「当前游戏是原神且有数据」时出现。</summary>
    public bool ShowGenshin => IsGenshin && Genshin is not null;

    /// <summary>绝区零分区只在「当前游戏是绝区零且有数据」时出现。</summary>
    public bool ShowZzz => IsZzz && Zzz is not null;

    [ObservableProperty]
    private string _fetchedAtText = "";


    /// <summary>Flyout 打开/点刷新时拉一次（Starward 的 <c>RefreshDailyNoteCommand</c>）。</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!HasCredentials)
        {
            ErrorText = null;
            StatusText = Strings["DailyNote.NoCookie"];
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorText = null;
        try
        {
            var role = new GameRecordRole
            {
                Nickname = Nickname,
                Uid = long.TryParse(_uid, out var parsed) ? parsed : 0,
                Region = _region,
                GameBiz = _gameBiz,
                Cookie = _cookie,
            };

            var summary = await _service.FetchAsync(_gameKind, new GameBiz(_gameBiz), role).ConfigureAwait(true);
            Apply(summary);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }


    private void Apply(DailyNoteSummary summary)
    {
        Nickname = summary.Nickname;
        RegionText = summary.Region ?? _region;
        LevelText = "";
        // summary.PrimaryName 是领域层中文；界面按当前语言取名字（en-US 不能出中文）。
        PrimaryName = IsGenshin ? Strings["DailyNote.Resin"] : Strings["DailyNote.Energy"];
        PrimaryCurrent = summary.PrimaryCurrent;
        PrimaryMax = summary.PrimaryMax;
        PrimaryPercent = summary.PrimaryPercent;
        RecoveryText = summary.PrimaryRecovery is { } recovery && recovery > TimeSpan.Zero
            ? string.Format(Strings["DailyNote.Recovery"], (int)recovery.TotalHours, recovery.Minutes)
            : Strings["DailyNote.Full"];
        Genshin = summary.Genshin;
        Zzz = summary.Zzz;
        FetchedAtText = summary.FetchedAt.ToString("HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
        StatusText = "";
    }


    /// <summary>没有 cookie / 想改 cookie：跳到工具箱（GameRecordPage 的登录入口）。</summary>
    [RelayCommand]
    private void OpenToolbox() => _context.Shell.Navigate(ShellPage.Toolbox);
}
