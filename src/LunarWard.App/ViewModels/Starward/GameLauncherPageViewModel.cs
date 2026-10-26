using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.App.ViewModels;
using LunarWard.App.Views;
using LunarWard.App.Views.Starward;
using LunarWard.Core.GameNotice;
using LunarWard.Core.HoYoPlay;
using LunarWard.Games;
using LunarWard.Core;
using LunarWard.Installer;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 启动器页（复刻 Starward <c>GameLauncherPage</c>）：
/// 公告卡（Banner + 活动/公告/资讯）、右下开始按钮、时长胶囊、右上便笺/账号、
/// 底部背景切换条。
/// </summary>
/// <remarks>
/// 像素布局见 <c>docs/07-UI-REPLICA.md</c> §2；启动相位驱动按钮文案见 §4.3。
/// LunarWard 追加：外部程序入口（Starward 没有这个页面，放进启动器页的浮层）。
/// </remarks>
public sealed partial class GameLauncherPageViewModel : ObservableObject, IDisposable
{
    private readonly PageContext _context;

    private readonly AppServices _services;

    private readonly LogService _log;

    private readonly DispatcherTimer _bannerTimer;

    private bool _disposed;

    /// <summary>资讯加载的「游戏代次」：只允许最新一代的结果落到 UI 上。</summary>
    private readonly ContentGenerationGate _contentGate = new();


    public GameLauncherPageViewModel(PageContext context)
    {
        _context = context;
        _services = context.Services;
        _log = context.Log;
        GameBiz = context.Shell.CurrentGameBiz;

        Banners = new ObservableCollection<BannerViewModel>();
        Posts = new ObservableCollection<PostGroupViewModel>();
        BackgroundImages = [];

        _bannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _bannerTimer.Tick += (_, _) => NextBanner();

        _services.LauncherService.StateChanged += OnStateChanged;
        _services.LauncherService.Completed += OnCompleted;
        _services.Installer.TaskStateChanged += OnInstallTaskStateChanged;
        _services.Installer.Progress = new Progress<GameInstallProgress>(progress
            => Dispatcher.UIThread.Post(() => ApplyInstallProgress(progress)));

        ApplyState(_services.LauncherService.State);
        UpdatePlayTime();
        DailyNote = new DailyNoteButtonViewModel(context);
        AuthLogin = new GameAuthLoginViewModel(context);

        _ = LoadContentAsync();
        _ = UpdateNoticeAlertAsync();
        _ = CheckUpdateAsync();
        RefreshBackgroundImages();
        LoadGameAccounts();
    }


    // ── 游戏 ──────────────────────────────────────────────────────────

    public string GameBiz { get; }

    /// <summary>XAML 里的文案索引器（<c>{Binding Strings[Launcher.StartGame]}</c>）。</summary>
    public Localization.AppStrings Strings => Localization.AppStrings.Instance;

    public string GameName => _context.Profile.DisplayName;

    public bool IsGenshin => GameBiz.StartsWith("hk4e", StringComparison.Ordinal);


    // ── 公告卡 ────────────────────────────────────────────────────────

    public ObservableCollection<BannerViewModel> Banners { get; }

    public ObservableCollection<PostGroupViewModel> Posts { get; }

    [ObservableProperty]
    private BannerViewModel? _currentBanner;

    [ObservableProperty]
    private int _currentBannerIndex;

    [ObservableProperty]
    private bool _showBannerCounter;

    [ObservableProperty]
    private string _bannerCounterText = "";

    [ObservableProperty]
    private bool _hasContent;

    [ObservableProperty]
    private string _contentStatus = "";


    public string CurrentPivotHeader => Posts.FirstOrDefault()?.Header ?? Strings["Launcher.Activity"];


    /// <summary>当前选中的资讯分组（Starward Pivot 的 SelectedIndex）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPosts))]
    private PostGroupViewModel? _selectedPostGroup;


    public IReadOnlyList<PostItemViewModel> CurrentPosts => SelectedPostGroup?.Items ?? [];


    [RelayCommand]
    private void SelectPivot(PostGroupViewModel group)
    {
        foreach (var item in Posts)
        {
            item.IsSelected = ReferenceEquals(item, group);
        }

        SelectedPostGroup = group;
    }


    [RelayCommand]
    public void NextBanner()
    {
        if (Banners.Count == 0)
        {
            return;
        }

        CurrentBannerIndex = (CurrentBannerIndex + 1) % Banners.Count;
    }


    /// <summary>上一张公告（Starward FlipView 的向左翻页）。</summary>
    public void PreviousBanner()
    {
        if (Banners.Count == 0)
        {
            return;
        }

        CurrentBannerIndex = (CurrentBannerIndex - 1 + Banners.Count) % Banners.Count;
    }


    partial void OnCurrentBannerIndexChanged(int value)
    {
        if (value >= 0 && value < Banners.Count)
        {
            CurrentBanner = Banners[value];
            BannerCounterText = $"{value + 1}/{Banners.Count}";
        }
    }


    [RelayCommand]
    private async Task OpenBannerLinkAsync()
    {
        if (CurrentBanner?.Link is { Length: > 0 } link)
        {
            await _services.OpenUriAsync(link).ConfigureAwait(true);
        }
    }


    private async Task LoadContentAsync()
    {
        // 切游戏时页面会重建，但旧页面的 HTTP 请求可能比新页面更晚返回；用代次令牌把过期结果丢掉，
        // 取不到内容时也清空而不是沿用上一个游戏的数据。
        var generation = _contentGate.Next();
        var launcherId = _context.Shell.CurrentLauncherId;
        var gameBiz = GameBiz;

        ResetContent();
        ContentStatus = Strings["Launcher.LoadingContent"];
        try
        {
            var assets = _context.Shell.Assets;
            var content = await assets.GetContentAsync(launcherId, gameBiz).ConfigureAwait(true);
            if (_disposed || !_contentGate.IsCurrent(generation))
            {
                return;
            }

            if (content is null)
            {
                ContentStatus = Strings["Launcher.OfflineContent"];
                return;
            }

            foreach (var banner in content.Banners ?? [])
            {
                var bitmap = await assets.BitmapAsync(banner.Image?.Url).ConfigureAwait(true);
                if (_disposed || !_contentGate.IsCurrent(generation))
                {
                    return;
                }

                if (bitmap is not null)
                {
                    Banners.Add(new BannerViewModel(bitmap, banner.Image?.Link));
                }
            }

            var groups = new (string Header, string Type)[]
            {
                (Strings["Launcher.Activity"], GamePostType.POST_TYPE_ACTIVITY),
                (Strings["Launcher.Announcement"], GamePostType.POST_TYPE_ANNOUNCE),
                (Strings["Launcher.Info"], GamePostType.POST_TYPE_INFO),
            };
            foreach (var (header, type) in groups)
            {
                var posts = (content.Posts ?? []).Where(x => x.Type == type).Take(50)
                    .Select(x => new PostItemViewModel(x.Title, x.Date, x.Link)).ToList();
                Posts.Add(new PostGroupViewModel(header, posts));
            }

            if (Posts.Count > 0)
            {
                SelectPivot(Posts[0]);
            }

            if (Banners.Count > 0)
            {
                CurrentBanner = Banners[0];
                CurrentBannerIndex = 0;
                BannerCounterText = $"1/{Banners.Count}";
                _bannerTimer.Start();
            }

            HasContent = Banners.Count > 0 || Posts.Any(x => x.Items.Count > 0);
            ContentStatus = HasContent ? "" : Strings["Launcher.NoContent"];
        }
        catch (Exception ex)
        {
            if (_disposed || !_contentGate.IsCurrent(generation))
            {
                return;
            }

            _log.Warn($"启动器页资讯加载失败：{ex.Message}");
            ResetContent();
            ContentStatus = Strings["Launcher.ContentFailed"];
        }
        finally
        {
            if (_contentGate.IsCurrent(generation))
            {
                OnPropertyChanged(nameof(CurrentPivotHeader));
            }
        }
    }


    /// <summary>清空公告卡上可能属于「上一个游戏」的数据（切游戏 / 加载失败时调用）。</summary>
    private void ResetContent()
    {
        _bannerTimer.Stop();
        Banners.Clear();
        Posts.Clear();
        CurrentBanner = null;
        CurrentBannerIndex = 0;
        BannerCounterText = "";
        ShowBannerCounter = false;
        SelectedPostGroup = null;
        HasContent = false;
    }


    /// <summary>Banner 区域鼠标进入：停止自动轮播并显示计数器（Starward 行为）。</summary>
    public void OnBannerPointerEnter()
    {
        _bannerTimer.Stop();
        ShowBannerCounter = true;
    }

    public void OnBannerPointerExit()
    {
        if (Banners.Count > 1)
        {
            _bannerTimer.Start();
        }

        ShowBannerCounter = false;
    }


    // ── 开始按钮 / 相位 ───────────────────────────────────────────────

    [ObservableProperty]
    private string _startButtonText = "";

    [ObservableProperty]
    private bool _isStartButtonEnabled = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAccentBackground), nameof(StartButtonForeground), nameof(SettingButtonForeground), nameof(ShowGameInfoPopup))]
    private bool _isRunning;

    /// <summary>
    /// Starward <c>StartGameButton</c>：可点且不是安装中 → 强调色底 + 强调色上的文字
    /// （见 <c>IsAccentColorBackgroundVisible</c> / <c>ActionButtonForeground</c>）。
    /// </summary>
    public bool IsAccentBackground => IsStartButtonEnabled && !IsRunning && !IsBusy && !IsInstalling;

    public Avalonia.Media.IBrush StartButtonForeground => IsAccentBackground
        ? Avalonia.Media.Brushes.Black
        : Avalonia.Media.Brush.Parse("#FFFFFFFF");

    public Avalonia.Media.IBrush SettingButtonForeground => IsAccentBackground
        ? Avalonia.Media.Brushes.Black
        : Avalonia.Media.Brush.Parse("#FFFFFFFF");


    // ── 预下载（Starward PreDownloadButton.xaml）──────────────────────

    /// <summary>
    /// Starward <c>IsPredownloadButtonEnabled</c> 的来源：HoYoPlay 上带 pre_download 包时才有这颗按钮，
    /// 且源码把 Visibility 直接绑 enabled——本地版本已追上预下载版本时按钮整颗消失。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPredownloadButton))]
    private bool _isPredownloadAvailable;

    /// <summary>Starward <c>CheckPreDownloadFinishedAsync</c>：本地版本 ≥ 预下载版本。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPredownloadButton), nameof(PredownloadButtonEnabled),
        nameof(PredownloadText), nameof(PredownloadInstallStateText), nameof(IsPredownloadPopupOpen))]
    private bool _isPredownloadFinished;

    /// <summary>Starward 的 <c>PointerOver</c>：悬停换「播放 / 暂停」图标并弹出下载信息。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PredownloadIcon), nameof(IsPredownloadPopupOpen))]
    private bool _predownloadPointerOver;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PredownloadIcon), nameof(PredownloadText),
        nameof(PredownloadButtonEnabled), nameof(PredownloadInstallStateText), nameof(IsPredownloadPopupOpen))]
    private GameInstallState _predownloadState = GameInstallState.Stop;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PredownloadText))]
    private string _predownloadPercentText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPredownloadBytes))]
    private string? _predownloadBytesText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPredownloadSpeed))]
    private string? _predownloadSpeedText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPredownloadRemain))]
    private string? _predownloadRemainText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPredownloadError))]
    private string? _predownloadErrorMessage;


    public bool ShowPredownloadButton => IsPredownloadAvailable && !IsPredownloadFinished;

    public bool PredownloadButtonEnabled => PredownloadButtonState.ButtonEnabled(PredownloadState, IsPredownloadFinished);

    /// <summary>Starward <c>GetButtonIcon</c>：下载 EBD3 / 悬停待续播 F5B0 / 悬停暂停 E62E。</summary>
    public string PredownloadIcon => PredownloadButtonState.Icon(PredownloadState, PredownloadPointerOver);

    /// <summary>Starward <c>GetButtonText</c>：预下载 / 等待中 / 百分比 / 已暂停 / 已完成 / 出错了。</summary>
    public string PredownloadText
        => PredownloadButtonState.Text(PredownloadState, IsPredownloadFinished, PredownloadPercentText, key => Strings[key]);

    /// <summary>Popup 里的状态行（Starward <c>GetInstallStateText</c>）。</summary>
    public string PredownloadInstallStateText
        => PredownloadButtonState.InstallStateText(PredownloadState, IsPredownloadFinished, key => Strings[key]);

    public bool HasPredownloadBytes => !string.IsNullOrEmpty(PredownloadBytesText);

    public bool HasPredownloadSpeed => !string.IsNullOrEmpty(PredownloadSpeedText);

    public bool HasPredownloadRemain => !string.IsNullOrEmpty(PredownloadRemainText);

    public bool HasPredownloadError => !string.IsNullOrEmpty(PredownloadErrorMessage);

    public bool IsPredownloadPopupOpen
        => PredownloadButtonState.PopupOpen(PredownloadState, IsPredownloadFinished, PredownloadPointerOver);


    /// <summary>
    /// Starward 只在 <c>GameState.InstallGame</c> 时显示「已安装 · 定位游戏」。
    /// 我们对应的是"目录配了但找不到游戏 exe"——目录完全没配时显示另一句提示。
    /// （<c>GameLauncherPage.xaml.cs</c> 的 <c>InstalledLocateGameEnabled</c>）
    /// </summary>
    public bool ShowLocateGame
    {
        get
        {
            var path = ProfileConfig.ExpandPath(_context.Profile.Profile.Path);
            if (_captureInstalled)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var definition = Games.GameCatalog.Detect(path);
            return definition is null || !File.Exists(Path.Join(path, definition.Executable));
        }
    }


    /// <summary>目录完全没配：按钮下方给一句"去游戏设置里指定目录"的小字。</summary>
    /// <summary>
    /// 用 DX12 启动（Starward 启动器右下角 <c>CompactCheckBox</c>「用 DX12 启动」，
    /// 对应 app 配置 <c>enable_dx12_{biz}</c>；LunarWard 存在 profile 的
    /// <c>enhancements.dx12</c> 里，启动时追加 <c>-use-d3d12</c>）。
    /// </summary>
    public bool EnableDx12
    {
        get => _context.Profile.Profile.Enhancements.Dx12;
        set
        {
            if (value == EnableDx12)
            {
                return;
            }

            _context.Profile.Profile.Enhancements.Dx12 = value;
            _services.Config.Save(_services.Paths.ConfigFile);
            OnPropertyChanged();
            _log.Info($"DX12 启动：{value}");
        }
    }


    /// <summary>Starward 依据 <c>enable_dx_switch</c> 或已开启的配置显示；这里在已配置游戏路径时显示。</summary>
    private bool _dx12SwitchSupported;


    /// <summary>
    /// Starward <c>CheckDX12ConfigAsync</c>：只有已开启、或 HoYoPlay 的 <c>enable_dx_switch</c> 为真时
    /// 才显示右下角的 DX12 开关（本项取不到配置就不显示）。
    /// </summary>
    private async Task CheckDx12ConfigAsync()
    {
        try
        {
            if (EnableDx12)
            {
                _dx12SwitchSupported = true;
                OnPropertyChanged(nameof(ShowDx12Option));
                return;
            }

            var gameId = LunarWard.Core.HoYoPlay.GameId.FromGameBiz(new LunarWard.Core.GameBiz(GameBiz));
            var launcherId = gameId is null ? null : LunarWard.Core.HoYoPlay.LauncherId.FromGameId(gameId);
            if (gameId is null || launcherId is null)
            {
                return;
            }

            var configs = await new LunarWard.Core.HoYoPlay.HoYoPlayClient()
                .GetDXConfigsAsync(launcherId, "zh-cn", [gameId], []);
            _dx12SwitchSupported = configs.FirstOrDefault(x => x.GameId.Id == gameId.Id)?.EnableDXSwitch is true;
            OnPropertyChanged(nameof(ShowDx12Option));
        }
        catch
        {
            // 取不到 DX 配置时保持隐藏（与源码 catch 后不显示一致）
        }
    }


    public bool ShowDx12Option => !ShowNotConfigured && _dx12SwitchSupported;


    public bool ShowNotConfigured => !_captureInstalled && string.IsNullOrWhiteSpace(
        ProfileConfig.ExpandPath(_context.Profile.Profile.Path));


    /// <summary>
    /// 公告铃铛是否显示（Starward <c>AppConfig.ShowGameAnnouncement</c>，默认开）。
    /// 设置弹窗里的开关会调 <see cref="RefreshNoticeSettings"/> 立即生效。
    /// </summary>
    public bool ShowGameNoticeBell => _context.Services.Config.Launcher.ShowGameNotice;

    /// <summary>红点是否被用户关掉（Starward <c>DisableRedDotReminderForGameNotices</c>）。</summary>
    public bool IsNoticeRedDotDisabled => _context.Services.Config.Launcher.DisableNoticeRedDot;

    /// <summary>设置弹窗改了公告相关开关后刷新铃铛。</summary>
    public void RefreshNoticeSettings()
    {
        OnPropertyChanged(nameof(ShowGameNoticeBell));
        OnPropertyChanged(nameof(IsNoticeRedDotDisabled));
        if (IsNoticeRedDotDisabled)
        {
            HasNoticeAlert = false;
        }
        else
        {
            _ = UpdateNoticeAlertAsync();
        }
    }

    /// <summary>公告铃铛红点：Starward 需要拉 GameNotice，这里用"有公告即亮"的近似。</summary>
    [ObservableProperty]
    private bool _hasNoticeAlert;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private string _runningInfo = "";

    [ObservableProperty]
    private string _runningTime = "00:00:00";

    private DateTimeOffset? _runningSince;


    [RelayCommand]
    private async Task StartGameAsync()
    {
        // Starward ChangeGameInstallTaskStateAsync：安装中点击按钮 = 暂停 / 继续 / 取消。
        if (IsInstalling)
        {
            ToggleInstall();
            return;
        }

        // Starward：GameState.InstallGame 时按钮是「安装游戏」，点击打开安装对话框而不是启动游戏。
        if (!IsGameInstalled)
        {
            InstallDialogRequested?.Invoke();
            return;
        }

        // Starward：GameState.UpdateGame 时按钮是「更新游戏」，直接按现有安装目录起更新，
        // 不弹「选择安装路径」对话框（安装目录和语音包都能从已安装的游戏里读出来）。
        if (NeedsUpdate)
        {
            await StartUpdateAsync();
            return;
        }

        var launcher = _services.LauncherService;
        if (launcher.State == LaunchPhaseState.Running)
        {
            launcher.RequestStop();
            StatusText = Strings["Launcher.Stopping"];
            return;
        }

        ErrorText = null;
        var profile = _context.Profile;
        var result = await launcher.StartAsync(profile.Id, profile.Profile).ConfigureAwait(true);
        if (!result.Success && result.Error is { } error)
        {
            ErrorText = error.Message;
            _log.Warn($"启动失败（{error.ExitCode}）：{error.Message}");
        }
    }


    private void OnStateChanged(object? sender, LaunchPhaseState state)
        => Dispatcher.UIThread.Post(() => ApplyState(state));

    private void OnCompleted(object? sender, LaunchResult result)
        => Dispatcher.UIThread.Post(() =>
        {
            _runningSince = null;
            UpdatePlayTime();
            StatusText = result.Success
                ? (result.Signal is { } signal
                    ? string.Format(Strings["Launcher.GameExitedWithSignal"], signal)
                    : Strings["Launcher.GameExited"])
                : "";
            if (!result.Success)
            {
                ErrorText = result.Error?.Message;
            }
        });


    private void ApplyState(LaunchPhaseState state)
    {
        var (text, enabled) = LaunchButtonState.For(state);
        // Starward 启动页按钮文案是「开始游戏 / 游戏正在运行」，相位细节放在状态小字里
        StartButtonText = state switch
        {
            LaunchPhaseState.Idle or LaunchPhaseState.Error when !IsGameInstalled => Strings["InstallGameDialog.Install"],
            LaunchPhaseState.Idle or LaunchPhaseState.Error when NeedsUpdate => Strings["InstallGameDialog.Update"],
            LaunchPhaseState.Idle or LaunchPhaseState.Error => Strings["Launcher.StartGame"],
            LaunchPhaseState.Running => Strings["Launcher.GameRunning"],
            _ => text,
        };
        IsStartButtonEnabled = enabled || (state is LaunchPhaseState.Idle or LaunchPhaseState.Error && (!IsGameInstalled || NeedsUpdate));
        IsBusy = state is LaunchPhaseState.Prepare or LaunchPhaseState.Launch
            or LaunchPhaseState.AwaitImage or LaunchPhaseState.AwaitReady or LaunchPhaseState.Cleanup;
        IsRunning = state == LaunchPhaseState.Running;
        StatusText = state switch
        {
            LaunchPhaseState.Idle => Strings["Main.StatusIdle"],
            LaunchPhaseState.Running => Strings["Main.StatusRunning"],
            LaunchPhaseState.Error => Strings["Main.StatusError"],
            _ => text,
        };

        if (state == LaunchPhaseState.Running)
        {
            _runningSince ??= DateTimeOffset.Now;
            RunningInfo = _services.LauncherService.GamePid is { } pid
                ? $"PID {pid}"
                : Strings["Main.StatusRunning"];
            _services.Background.Pause();
        }
        else if (state == LaunchPhaseState.Idle)
        {
            _runningSince = null;
            RunningInfo = "";
            _services.Background.Resume();
        }
    }


    /// <summary>悬停在整颗开始按钮上时打开「游戏信息 / 下载进度」卡片（Starward Popup_GameInfoOrDownloadProgress）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGameInfoPopup))]
    private bool _isGameInfoPopupOpen;


    /// <summary>只有运行中或安装中才显示这张卡片（源码 PointerEntered 里同一判断）。</summary>
    public bool ShowGameInfoPopup => IsGameInfoPopupOpen && (IsRunning || IsInstalling);


    /// <summary>运行中显示的「进程名 (PID)」。</summary>
    public string RunningGameInfo => RunningInfo ?? "";


    public bool HasDownloadBytes => !string.IsNullOrEmpty(DownloadBytesText);

    public bool HasInstallBytes => !string.IsNullOrEmpty(InstallBytesText);

    public bool HasVerifySpeed => !string.IsNullOrEmpty(VerifySpeedText);


    // ── 安装 / 更新（Starward StartGameButton：按钮里的进度圈 / 悬停暂停继续）──

    /// <summary>视图订阅它来弹「选择安装路径」对话框（Starward 在页面代码里 ShowAsync）。</summary>
    public event Action? InstallDialogRequested;


    /// <summary>
    /// 视图订阅它来弹预下载确认对话框（Starward <c>PreDownloadDialog</c>，参数是启动器 id / gameBiz / 安装路径）。
    /// </summary>
    public event Action<PredownloadDialogArgs>? PredownloadDialogRequested;

    /// <summary>视图订阅它来弹「游戏设置」对话框（Starward 在页面代码里 ShowAsync）。</summary>
    public event Action? SettingDialogRequested;

    /// <summary>视图订阅它来开目录选择器（定位游戏）。</summary>
    public event Action? LocateGameRequested;

    /// <summary>网络驱动器等需要提示给用户的消息（页面上方的状态行）。</summary>
    public void ReportStatus(string message)
    {
        StatusText = message;
        ErrorText = null;
    }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAccentBackground), nameof(ShowGameInfoPopup))]
    private bool _isInstalling;

    /// <summary>Starward <c>InstallState</c>：按钮里的进度圆环与悬停文案都看它。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallStateIsPending),
        nameof(InstallStateIsDownloading),
        nameof(ButtonDownloadingRemainTimeOpacity),
        nameof(TextBlockInstallStateOpacity),
        nameof(TextBlockPointerOverOpacity),
        nameof(TextBlockInstallStateText),
        nameof(TextBlockPointerOverText),
        nameof(InstallStateText))]
    private GameInstallState _installState = GameInstallState.Stop;

    /// <summary>Starward <c>ActionButtonPointerOver</c>：悬停时把状态行换成「暂停 / 继续 / 取消」。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonDownloadingRemainTimeOpacity),
        nameof(TextBlockInstallStateOpacity),
        nameof(TextBlockPointerOverOpacity),
        nameof(TextBlockInstallStateText),
        nameof(TextBlockPointerOverText))]
    private bool _actionButtonPointerOver;


    public bool InstallStateIsPending => InstallState is GameInstallState.Waiting;

    public bool InstallStateIsDownloading => InstallState is GameInstallState.Downloading;

    /// <summary>「下载中 + 剩余时间」两行：只在下载中、且鼠标没悬停时显示。</summary>
    public double ButtonDownloadingRemainTimeOpacity
        => !ActionButtonPointerOver && InstallState is GameInstallState.Downloading ? 1 : 0;

    /// <summary>「已暂停 / 出错 / 队列中」单行：非下载中、且鼠标没悬停时显示。</summary>
    public double TextBlockInstallStateOpacity
        => !ActionButtonPointerOver && InstallState is not GameInstallState.Downloading ? 1 : 0;

    /// <summary>悬停时的「暂停 / 继续 / 取消」。</summary>
    public double TextBlockPointerOverOpacity => ActionButtonPointerOver ? 1 : 0;

    public string TextBlockInstallStateText => (ActionButtonPointerOver, InstallState) switch
    {
        (false, GameInstallState.Waiting) => Strings["StartGameButton.Waiting"],
        (false, GameInstallState.Decompressing) => Strings["DownloadGamePage.Decompressing"],
        (false, GameInstallState.Merging) => Strings["DownloadGamePage.Merging"],
        (false, GameInstallState.Verifying) => Strings["DownloadGamePage.Verifying"],
        (false, GameInstallState.Paused) => Strings["DownloadGamePage.Paused"],
        (false, GameInstallState.Error) => Strings["Common.Error"],
        (false, GameInstallState.Queueing) => Strings["StartGameButton.InQueue"],
        _ => "",
    };

    public string TextBlockPointerOverText => (ActionButtonPointerOver, InstallState) switch
    {
        (true, GameInstallState.Waiting or GameInstallState.Downloading) => Strings["StartGameButton.Pause"],
        (true, GameInstallState.Paused or GameInstallState.Error or GameInstallState.Queueing) => Strings["Common.Continue"],
        (true, GameInstallState.Decompressing or GameInstallState.Merging or GameInstallState.Verifying) => Strings["Common.Cancel"],
        _ => "",
    };

    /// <summary>按钮里进度圈下方的状态文案（Starward <c>InstallStateText</c>）。</summary>
    public string InstallStateText => InstallState switch
    {
        GameInstallState.Waiting => Strings["StartGameButton.Waiting"],
        GameInstallState.Downloading => Strings["DownloadGamePage.Downloading"],
        GameInstallState.Decompressing => Strings["DownloadGamePage.Decompressing"],
        GameInstallState.Merging => Strings["DownloadGamePage.Merging"],
        GameInstallState.Verifying => Strings["DownloadGamePage.Verifying"],
        GameInstallState.Paused => Strings["DownloadGamePage.Paused"],
        GameInstallState.Finish => Strings["DownloadGamePage.Finished"],
        GameInstallState.Error => Strings["DownloadGamePage.SomethingError"],
        GameInstallState.Queueing => Strings["StartGameButton.InQueue"],
        _ => "",
    };


    /// <summary>进度圆环中央的整数百分比。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RingSweepAngle))]
    private int _progressRingValue;

    /// <summary>Avalonia 没有 ProgressRing，用 Arc 的 SweepAngle 画圆环（0~360）。</summary>
    public double RingSweepAngle => Math.Clamp(ProgressRingValue, 0, 100) * 3.6;

    [ObservableProperty]
    private string _progressPercentText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDownloadBytes))]
    private string? _downloadBytesText;

    [ObservableProperty]
    private string? _downloadSpeedText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstallBytes))]
    private string? _installBytesText;

    [ObservableProperty]
    private string? _installSpeedText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVerifySpeed))]
    private string? _verifySpeedText;

    [ObservableProperty]
    private string? _remainTimeText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstallError))]
    private string? _installErrorMessage;

    /// <summary>Starward 在进度卡里用红色小字显示安装错误。</summary>
    public bool HasInstallError => !string.IsNullOrWhiteSpace(InstallErrorMessage);


    private GameInstallRequest? _installRequest;

    /// <summary>自检截图时的安装/更新语义（没有真实 request 时用）。</summary>
    private GameInstallOperation _captureOperation = GameInstallOperation.Install;

    /// <summary>自检截图时假装游戏已安装（截图机的 profile 里通常没有真实游戏目录）。</summary>
    private bool _captureInstalled;


    /// <summary>游戏是否已经装好（目录里能找到 exe）。</summary>
    public bool IsGameInstalled => _captureInstalled || (!ShowNotConfigured && !ShowLocateGame);

    /// <summary>本地版本低于 HoYoPlay 最新版本时为 true（Starward <c>GameState.UpdateGame</c>）。</summary>
    [ObservableProperty]
    private bool _needsUpdate;

    /// <summary>版本小字（如「5.1.0 → 5.2.0」），只在需要更新时显示。</summary>
    [ObservableProperty]
    private string _gameVersionText = "";

    /// <summary>本地已安装版本（读 config.ini）；没有安装时为 null。</summary>
    public Version? LocalGameVersion { get; private set; }


    /// <summary>
    /// 启动时对照 HoYoPlay 上的最新版本：本地 &lt; 最新 → 按钮变「更新游戏」。
    /// 失败（离线）时不改状态，保持「开始游戏」。
    /// </summary>
    private async Task CheckUpdateAsync()
    {
        try
        {
            if (!IsGameInstalled)
            {
                return;
            }

            var path = ProfileConfig.ExpandPath(_context.Profile.Profile.Path);
            var gameId = GameId.FromGameBiz(new GameBiz(GameBiz));
            if (string.IsNullOrWhiteSpace(path) || gameId is null || string.IsNullOrWhiteSpace(_context.Shell.CurrentLauncherId))
            {
                return;
            }

            LocalGameVersion = await _services.Installer.GetLocalGameVersionAsync(path).ConfigureAwait(true);
            var package = await _services.Installer.GetGamePackageAsync(
                _context.Shell.CurrentLauncherId,
                string.IsNullOrWhiteSpace(Strings.Language) ? "zh-cn" : Strings.Language.ToLowerInvariant(),
                new GameId { GameBiz = GameBiz, Id = gameId.Id }).ConfigureAwait(true);

            if (package?.Main?.Major?.Version is { } latestText
                && Version.TryParse(latestText, out var latest)
                && LocalGameVersion is { } local)
            {
                var needsUpdate = latest > local;
                GameVersionText = needsUpdate ? $"{local} → {latest}" : "";
                NeedsUpdate = needsUpdate;
                ApplyState(_services.LauncherService.State);
                _log.Info($"版本对比：本地 {local}，最新 {latest}，需要更新={needsUpdate}");
            }

            // 预下载：源码 CheckPreDownloadFinishedAsync 用本地版本和 pre_download 版本比
            if (package?.PreDownload?.Major?.Version is { Length: > 0 } preText && Version.TryParse(preText, out var pre))
            {
                IsPredownloadAvailable = true;
                IsPredownloadFinished = LocalGameVersion is { } localVersion && localVersion >= pre;
            }
            else
            {
                IsPredownloadAvailable = false;
                IsPredownloadFinished = false;
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"检查游戏更新失败（{GameBiz}）：{ex.Message}");
        }
    }


    /// <summary>自检截图专用：把按钮切到「安装中」并填一份假进度（不触发真实网络）。</summary>
    public void ShowInstallProgressForCapture(GameInstallOperation operation = GameInstallOperation.Install)
    {
        _captureOperation = operation;
        _captureInstalled = true;
        OnPropertyChanged(nameof(IsGameInstalled));
        OnPropertyChanged(nameof(ShowNotConfigured));
        OnPropertyChanged(nameof(ShowLocateGame));
        IsInstalling = true;
        InstallState = GameInstallState.Downloading;
        ProgressRingValue = 46;
        ProgressPercentText = "46.4%";
        DownloadBytesText = "4.32/9.31 GB";
        DownloadSpeedText = "18.20 MB/s";
        InstallBytesText = "3.10/6.02 GB";
        InstallSpeedText = "25.00 MB/s";
        VerifySpeedText = "42.00 MB/s";
        RemainTimeText = "00:03:20";
        InstallErrorMessage = null;
        ActionButtonPointerOver = CaptureMode.InstallHover;
        // 悬停整颗按钮 → 打开「游戏信息 / 下载进度」卡片（截图要看得见）
        IsGameInfoPopupOpen = CaptureMode.InstallHover || CaptureMode.InstallInfoPopup;
        OnPropertyChanged(nameof(IsAccentBackground));
    }


    /// <summary>
    /// 自检截图专用：摆出预下载按钮的各种状态（idle / downloading / hover / finished），
    /// 不触发真实网络（真实可用性靠 <see cref="CheckUpdateAsync" /> 拉 HoYoPlay 包）。
    /// </summary>
    public void ShowPredownloadForCapture(string state)
    {
        IsPredownloadAvailable = state is not "finished";
        IsPredownloadFinished = state is "finished";
        PredownloadState = state switch
        {
            "downloading" or "hover" => GameInstallState.Downloading,
            _ => GameInstallState.Stop,
        };
        PredownloadPercentText = "38.6%";
        PredownloadBytesText = state is "downloading" or "hover" ? "3.51/9.09 GB" : null;
        PredownloadSpeedText = state is "downloading" or "hover" ? "22.40 MB/s" : null;
        PredownloadRemainText = state is "downloading" or "hover" ? "00:04:12" : null;
        PredownloadErrorMessage = null;
        PredownloadPointerOver = state is "hover";
    }


    /// <summary>自检截图专用：假装游戏已安装且需要更新，按钮切到「更新游戏」（强调色底）。</summary>
    public void ShowUpdateButtonForCapture()
    {
        _captureInstalled = true;
        NeedsUpdate = true;
        GameVersionText = "5.1.0 → 5.2.0";
        OnPropertyChanged(nameof(IsGameInstalled));
        OnPropertyChanged(nameof(ShowNotConfigured));
        OnPropertyChanged(nameof(ShowLocateGame));
        ApplyState(_services.LauncherService.State);
    }


    /// <summary>
    /// Starward <c>UpdateGameAsync</c>：更新不需要弹「选择安装路径」，
    /// 直接按现有安装目录 + 已安装语音包开一个 Update 任务。
    /// </summary>
    private async Task StartUpdateAsync()
    {
        try
        {
            var path = ProfileConfig.ExpandPath(_context.Profile.Profile.Path);
            var gameId = GameId.FromGameBiz(new GameBiz(GameBiz));
            if (string.IsNullOrWhiteSpace(path) || gameId is null)
            {
                return;
            }

            var audio = await _services.Installer
                .GetAudioLanguageAsync(new GameId { GameBiz = GameBiz, Id = gameId.Id }, path)
                .ConfigureAwait(true);

            await BeginInstallAsync(new GameInstallRequest
            {
                GameBiz = GameBiz,
                GameId = gameId.Id,
                InstallPath = path,
                Operation = GameInstallOperation.Update,
                AudioLanguage = audio,
            });
        }
        catch (Exception ex)
        {
            _log.Error("Start update game failed", ex);
        }
    }


    /// <summary>由页面代码在安装对话框确认后调用：开始下载安装。</summary>
    public async Task BeginInstallAsync(GameInstallRequest request)
    {
        _captureOperation = request.Operation;
        _installRequest = request;
        // 预下载不占用「开始游戏」按钮的安装态（Starward 里两者是独立按钮）
        IsInstalling = request.Operation is not GameInstallOperation.Predownload;
        if (request.Operation is GameInstallOperation.Predownload)
        {
            PredownloadState = GameInstallState.Waiting;
            PredownloadPercentText = "0%";
            PredownloadBytesText = null;
            PredownloadSpeedText = null;
            PredownloadRemainText = "--:--:--";
            PredownloadErrorMessage = null;
        }
        InstallState = GameInstallState.Waiting;
        ProgressRingValue = 0;
        ProgressPercentText = "0%";
        DownloadBytesText = null;
        DownloadSpeedText = null;
        InstallBytesText = null;
        InstallSpeedText = null;
        VerifySpeedText = null;
        RemainTimeText = "--:--:--";
        InstallErrorMessage = null;
        OnPropertyChanged(nameof(IsAccentBackground));

        var progress = _services.Installer.StartOrContinue(request);
        ApplyInstallProgress(progress);

        // 服务在 StartOrContinue 内已经启动了唯一一条流水线（审计 M-03）：
        // 这里 await 它即可，不能再 Task.Run 第二份。
        if (_services.Installer.CurrentRun is { } run)
        {
            await run.ConfigureAwait(true);
        }
    }


    /// <summary>
    /// Starward <c>ChangeGameInstallTaskStateAsync</c>：安装中点击按钮——
    /// 下载中/等待中 → 暂停；已暂停/出错/队列中 → 继续；解压/合并/校验 → 取消。
    /// </summary>
    private void ToggleInstall()
    {
        if (_installRequest is null)
        {
            return;
        }

        switch (InstallState)
        {
            case GameInstallState.Waiting or GameInstallState.Downloading:
                ApplyInstallProgress(_services.Installer.Pause(_installRequest));
                break;

            case GameInstallState.Paused or GameInstallState.Error or GameInstallState.Queueing:
                // 服务内部已单飞启动；这里只同步状态，不再重复起任务（审计 M-03）。
                ApplyInstallProgress(_services.Installer.StartOrContinue(_installRequest));
                break;

            case GameInstallState.Decompressing or GameInstallState.Merging or GameInstallState.Verifying:
                _services.Installer.Stop(_installRequest);
                _installRequest = null;
                IsInstalling = false;
                InstallState = GameInstallState.Stop;
                ApplyState(_services.LauncherService.State);
                break;
        }
    }


    private void OnInstallTaskStateChanged(object? sender, GameInstallContext context)
        => Dispatcher.UIThread.Post(() => ApplyInstallProgress(GameInstallProgress.FromTask(context)));


    /// <summary>
    /// Starward <c>UpdateGameInstallTaskState</c>：把安装器快照翻成按钮里的
    /// 百分比 / 速度 / 剩余时间（文案与阈值照抄上游，别改）。
    /// </summary>
    private void ApplyInstallProgress(GameInstallProgress progress)
    {
        // 预下载是「另一颗按钮」，不能碰 StartGameButton 的 IsInstalling / 进度圆环。
        if (_installRequest?.Operation is GameInstallOperation.Predownload)
        {
            ApplyPredownloadProgress(progress);
            return;
        }

        // 页面切走再切回来时，安装器仍在跑：靠第一份进度快照把「安装中」重新点亮。
        if (progress.State is not (int)GameInstallState.Stop and not (int)GameInstallState.Finish)
        {
            IsInstalling = true;
        }

        InstallState = (GameInstallState)progress.State;
        DownloadBytesText = ToBytesText(progress.ProgressDownloadFinishBytes, progress.ProgressDownloadTotalBytes);
        InstallBytesText = ToBytesText(progress.ProgressWriteFinishBytes, progress.ProgressWriteTotalBytes);
        InstallErrorMessage = progress.ErrorMessage;

        if (InstallState is GameInstallState.Downloading)
        {
            long total = progress.ProgressDownloadTotalBytes;
            long finish = progress.ProgressDownloadFinishBytes;
            double value = total > 0 ? (double)finish / total : 0;
            DownloadSpeedText = ToSpeedText(progress.NetworkDownloadSpeed);
            InstallSpeedText = ToSpeedText(progress.StorageWriteSpeed);
            VerifySpeedText = ToSpeedText(progress.StorageReadSpeed);
            RemainTimeText = ToRemainTimeText(progress.RemainTimeSeconds);
            if (_captureOperation is GameInstallOperation.Update
                && progress.DownloadMode is (int)GameInstallDownloadMode.Chunk
                && progress.ProgressWriteTotalBytes > 0)
            {
                value = (double)progress.ProgressWriteFinishBytes / progress.ProgressWriteTotalBytes;
            }

            ProgressRingValue = (int)(value * 100);
            ProgressPercentText = $"{value:P1}";
        }
        else if (InstallState is GameInstallState.Decompressing or GameInstallState.Merging)
        {
            DownloadSpeedText = null;
            InstallSpeedText = null;
            VerifySpeedText = null;
            RemainTimeText = "--:--:--";
            ProgressRingValue = (int)(progress.ProgressPercent * 100);
            ProgressPercentText = $"{progress.ProgressPercent:P1}";
        }
        else if (InstallState is GameInstallState.Finish)
        {
            DownloadSpeedText = null;
            InstallSpeedText = null;
            VerifySpeedText = null;
            RemainTimeText = null;
            InstallErrorMessage = null;
            ProgressRingValue = 100;
            ProgressPercentText = "100%";
        }
        else
        {
            DownloadSpeedText = null;
            InstallSpeedText = null;
            VerifySpeedText = null;
            RemainTimeText = "--:--:--";
            ProgressRingValue = (int)(progress.ProgressPercent * 100);
            ProgressPercentText = $"{progress.ProgressPercent:P1}";
        }

        if (InstallState is GameInstallState.Finish)
        {
            IsInstalling = false;
            _installRequest = null;
            OnInstallFinished(progress);
        }
        else if (InstallState is GameInstallState.Stop)
        {
            IsInstalling = false;
            _installRequest = null;
        }
    }


    /// <summary>
    /// Starward <c>PredownloadAsync</c>：有预下载任务就暂停 / 继续，否则先弹
    /// <c>PreDownloadDialog</c> 确认（带体积估算），确认后由页面回调 <see cref="StartPredownloadAsync"/>。
    /// </summary>
    [RelayCommand]
    private async Task PredownloadAsync()
    {
        if (IsPredownloadFinished)
        {
            return;
        }

        try
        {
            if (_installRequest is { } request && request.Operation is GameInstallOperation.Predownload)
            {
                switch (PredownloadState)
                {
                    case GameInstallState.Waiting or GameInstallState.Downloading:
                        ApplyPredownloadProgress(_services.Installer.Pause(request));
                        return;

                    case GameInstallState.Paused or GameInstallState.Error or GameInstallState.Queueing:
                        // 同 ToggleInstall：StartOrContinue 已启动唯一流水线（审计 M-03）。
                        ApplyPredownloadProgress(_services.Installer.StartOrContinue(request));
                        return;
                }
            }

            var path = ProfileConfig.ExpandPath(_context.Profile.Profile.Path);
            var gameId = GameId.FromGameBiz(new GameBiz(GameBiz));
            if (string.IsNullOrWhiteSpace(path) || gameId is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_context.Shell.CurrentLauncherId))
            {
                return;
            }
            // 源码先弹确认框（含体积估算），确认后再走 BeginInstallAsync
            PredownloadDialogRequested?.Invoke(new PredownloadDialogArgs(
                _context.Shell.CurrentLauncherId,
                GameBiz,
                path));
        }
        catch (Exception ex)
        {
            _log.Error("Predownload failed", ex);
        }
    }


    /// <summary>确认框点「开始预下载」后调用（页面的弹窗回调）。</summary>
    public Task StartPredownloadAsync(GameInstallRequest request) => BeginInstallAsync(request);


    /// <summary>把预下载任务的进度快照翻到按钮 / Popup 上（Starward <c>UpdateGameInstallTaskState</c>）。</summary>
    private void ApplyPredownloadProgress(GameInstallProgress progress)
    {
        PredownloadState = (GameInstallState)progress.State;
        PredownloadErrorMessage = progress.ErrorMessage;

        long total = progress.ProgressDownloadTotalBytes;
        long finish = progress.ProgressDownloadFinishBytes;
        double value = total > 0 ? (double)finish / total : progress.ProgressPercent;
        PredownloadPercentText = $"{value:P1}";
        PredownloadBytesText = total > 0 ? PredownloadButtonState.BytesText(finish, total) : null;

        if (PredownloadState is GameInstallState.Paused or GameInstallState.Queueing or GameInstallState.Error)
        {
            PredownloadSpeedText = "- KB/s";
            PredownloadRemainText = "--:--:--";
        }
        else if (PredownloadState is GameInstallState.Finish)
        {
            PredownloadSpeedText = null;
            PredownloadRemainText = null;
            PredownloadErrorMessage = null;
            IsPredownloadFinished = true;
            PredownloadState = GameInstallState.Stop;
            _installRequest = null;
        }
        else if (PredownloadState is GameInstallState.Stop)
        {
            PredownloadSpeedText = null;
            PredownloadRemainText = null;
            _installRequest = null;
        }
        else
        {
            PredownloadSpeedText = PredownloadButtonState.SpeedText(progress.NetworkDownloadSpeed);
            PredownloadRemainText = PredownloadButtonState.RemainTimeText(progress.RemainTimeSeconds);
        }
    }


    private void OnInstallFinished(GameInstallProgress progress)
    {
        // 安装完把 profile 的路径指向安装目录（Starward 里是 config 的 install_path），然后按钮变「开始游戏」。
        if (!string.IsNullOrWhiteSpace(progress.InstallPath))
        {
            _context.Profile.Profile.Path = progress.InstallPath;
            _services.Config.Save(_services.Paths.ConfigFile);
        }

        NeedsUpdate = false;
        GameVersionText = "";
        OnPropertyChanged(nameof(IsGameInstalled));
        OnPropertyChanged(nameof(ShowNotConfigured));
        OnPropertyChanged(nameof(ShowLocateGame));
        ApplyState(_services.LauncherService.State);
        StatusText = Strings["InstallGameDialog.Completed"];
        _log.Info($"安装完成：{progress.GameBiz} → {progress.InstallPath}");
    }


    private static string? ToBytesText(long finish, long total)
    {
        const double MB = 1 << 20;
        const double GB = 1 << 30;
        if (total == 0)
        {
            return null;
        }

        return total >= GB
            ? $"{finish / GB:F2}/{total / GB:F2} GB"
            : $"{finish / MB:F2}/{total / MB:F2} MB";
    }


    private static string ToSpeedText(long bytes)
    {
        const double KB = 1 << 10;
        const double MB = 1 << 20;
        return bytes >= MB ? $"{bytes / MB:F2} MB/s" : $"{bytes / KB:F2} KB/s";
    }


    private static string? ToRemainTimeText(long seconds)
    {
        if (seconds == 0)
        {
            return "--:--:--";
        }

        return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
    }


    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / 1024d / 1024d / 1024d:F2} GB",
        >= 1L << 20 => $"{bytes / 1024d / 1024d:F1} MB",
        >= 1L << 10 => $"{bytes / 1024d:F0} KB",
        _ => $"{bytes} B",
    };


    /// <summary>由视图的 1s 计时器驱动（Starward 启动按钮在运行时显示已运行时间）。</summary>
    public void TickRunningTime()
    {
        if (_runningSince is not { } since)
        {
            RunningTime = "";
            return;
        }

        var span = DateTimeOffset.Now - since;
        RunningTime = $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }


    // ── 时长 ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _playTimeText = "";


    /// <summary>档案 → 区服字符串（如 <c>hk4e_cn</c>），与上游 <c>PlayTimeStats.GameBiz</c> 同口径。</summary>
    public static string GameBizOfProfile(ProfileItemViewModel profileItem) => GameBizOfProfile(profileItem.Profile);

    public static string GameBizOfProfile(ProfileConfig profile)
    {
        var definition = Games.GameCatalog.Get(
            profile.Game,
            string.Equals(profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
                ? Games.GameEdition.Global
                : Games.GameEdition.China);
        return definition?.GameBiz ?? "hk4e_cn";
    }


    public void UpdatePlayTime()
    {
        var total = _services.PlayTime.TotalFor(GameBizOfProfile(_context.Profile));
        PlayTimeText = total <= TimeSpan.Zero
            ? "0h 0m"
            : $"{(int)total.TotalHours}h {total.Minutes}m";
    }


    // ── 背景切换条 ────────────────────────────────────────────────────

    public ObservableCollection<BackgroundSwitchItem> BackgroundImages { get; }

    [ObservableProperty]
    private int _selectedBackgroundIndex;

    [ObservableProperty]
    private bool _showBackgroundBar;

    [ObservableProperty]
    private bool _hasVideoBackground;


    private bool _backgroundImagesLoaded;

    /// <summary>背景目录加载的代次：旧加载晚到时直接丢弃，避免「新列表被旧结果覆盖」。</summary>
    private int _backgroundLoadGeneration;


    /// <summary>
    /// 刷新可切换背景（官方接口图/官方动态背景 + 版本海报 + 本地背景目录）。
    /// </summary>
    /// <remarks>
    /// 对照 Starward <c>BackgroundService.GetGameBackgroundsAsync</c> 与
    /// <c>GameLauncherPage.BackgroundImages</c>。接口失败时退化为海报 + 本地文件，不阻塞启动。
    /// </remarks>
    public void RefreshBackgroundImages()
    {
        // 拖入/删除本地背景后必须绕过「只加载一次」的守卫，否则新文件永远不出现。
        _ = LoadBackgroundImagesAsync(force: true);
    }


    public async Task LoadBackgroundImagesAsync(bool force = false)
    {
        if (_backgroundImagesLoaded && !force)
        {
            return;
        }

        _backgroundImagesLoaded = true;
        var generation = Interlocked.Increment(ref _backgroundLoadGeneration);

        // 记住当前选中的背景，重建列表后回到同一张（上游 AppBackground 也是恢复上次选择）。
        var previous = CurrentBackground?.Value;

        IReadOnlyList<BackgroundEntry> entries;
        var art = _context.Shell.Assets.For();
        var launcherId = _context.Shell.CurrentLauncherId;
        if (CaptureMode.BackgroundPath is { Length: > 0 } forcedBackground)
        {
            // 自检专用：强制指定背景（本地文件或 https 地址），验证 ffmpeg 解帧链路。
            entries = forcedBackground.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? [new BackgroundEntry(BackgroundKind.Custom, null, forcedBackground, "自检视频")]
                : BackgroundCatalog.Merge([], null, BackgroundCatalog.FromFiles([forcedBackground]));
        }
        else if (string.IsNullOrWhiteSpace(launcherId))
        {
            entries = BackgroundCatalog.Merge([], art?.Background,
                BackgroundCatalog.FromFiles(_services.Background.Discover()
                    .SelectMany(x => new[] { x.ImagePath, x.VideoPath })
                    .OfType<string>()));
        }
        else
        {
            entries = await _services.Background
                .LoadCatalogAsync(launcherId, GameBiz, art?.Background)
                .ConfigureAwait(true);
        }

        if (generation != Volatile.Read(ref _backgroundLoadGeneration))
        {
            // 页面重建/切游戏触发了更新的一次加载，旧结果直接丢弃。
            return;
        }

        BackgroundImages.Clear();
        foreach (var entry in entries)
        {
            BackgroundImages.Add(new BackgroundSwitchItem(
                entry.Title,
                entry.Value,
                entry.ImageSource,
                entry.VideoSource,
                entry.Kind == BackgroundKind.Official,
                entry.ThemeSource));
        }

        ShowBackgroundBar = BackgroundImages.Count > 1;
        HasVideoBackground = BackgroundImages.Any(x => x.IsVideo);

        if (BackgroundImages.Count == 0)
        {
            return;
        }

        var selected = 0;
        if (previous is { Length: > 0 })
        {
            for (var i = 0; i < BackgroundImages.Count; i++)
            {
                if (string.Equals(BackgroundImages[i].Value, previous, StringComparison.Ordinal))
                {
                    selected = i;
                    break;
                }
            }
        }

        await SelectBackgroundAsync(selected).ConfigureAwait(true);
    }


    /// <summary>当前选中的背景（图库/复制/另存为按它取文件）。</summary>
    public BackgroundSwitchItem? CurrentBackground
        => SelectedBackgroundIndex >= 0 && SelectedBackgroundIndex < BackgroundImages.Count
            ? BackgroundImages[SelectedBackgroundIndex]
            : null;


    public string GalleryDirectory => _services.Background.CacheDirectory;


    /// <summary>复制/另存为的失败信息回填（Starward 用 Toast，这里沿用页面错误行）。</summary>
    public void ReportError(string message) => ErrorText = message;


    public Task<Bitmap?> GetBitmapAsync(string value) => _context.Shell.Assets.BitmapAsync(value);


    /// <summary>Starward「图库」打开背景浏览窗；Linux 等价为打开背景缓存目录。</summary>
    [RelayCommand]
    private void OpenBackgroundFolder()
    {
        try
        {
            Directory.CreateDirectory(GalleryDirectory);
        }
        catch (Exception)
        {
            // 目录建立失败也照样尝试打开（可能已存在）
        }

        if (SettingShellActions.TryOpen(GalleryDirectory) is { } error)
        {
            ErrorText = error;
        }
    }


    partial void OnSelectedBackgroundIndexChanged(int value) => OnPropertyChanged(nameof(CurrentBackground));


    /// <summary>拖放图片/视频时顶部提示条（Starward <c>Border_BackgroundDragIn</c>）。</summary>
    [ObservableProperty]
    private bool _isDragOverlayVisible;


    /// <summary>
    /// 拖放进来的背景文件：拷进背景目录，刷新列表并选中它
    /// （Starward <c>BackgroundService.ChangeCustomBackgroundFileAsync</c> + <c>SetCustomBg</c>）。
    /// </summary>
    public async Task<string?> AdoptBackgroundFileAsync(string sourcePath)
    {
        try
        {
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".mp4" or ".webm" or ".mkv"))
            {
                return Strings["Launcher.DropBackgroundUnsupported"];
            }

            var directory = _services.Background.CacheDirectory;
            Directory.CreateDirectory(directory);

            var fileName = Path.GetFileName(sourcePath);
            var destination = Path.Join(directory, fileName);
            if (File.Exists(destination) && !string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destination), StringComparison.Ordinal))
            {
                fileName = $"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}{extension}";
                destination = Path.Join(directory, fileName);
            }

            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destination), StringComparison.Ordinal))
            {
                File.Copy(sourcePath, destination, true);
            }

            RefreshBackgroundImages();
            var index = BackgroundImages.ToList().FindIndex(x => string.Equals(x.Value, destination, StringComparison.Ordinal));
            if (index >= 0)
            {
                await SelectBackgroundAsync(index).ConfigureAwait(true);
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }


    /// <summary>视频背景的播放/暂停（Starward <c>StartOrStopVideoBackgroundCommand</c>）。</summary>
    [ObservableProperty]
    private bool _videoPaused;


    partial void OnVideoPausedChanged(bool value)
    {
        if (value)
        {
            _services.Background.Pause();
        }
        else
        {
            _services.Background.Resume();
        }

        OnPropertyChanged(nameof(VideoToggleGlyph));
    }


    partial void OnHasVideoBackgroundChanged(bool value)
    {
        OnPropertyChanged(nameof(CanToggleVideo));
    }


    public bool CanToggleVideo => HasVideoBackground;

    /// <summary>Starward <c>StartStopButtonIcon</c>：暂停时显示播放，播放中显示暂停。</summary>
    public string VideoToggleGlyph => VideoPaused ? "\uE768" : "\uE769";


    [RelayCommand]
    private void ToggleVideo()
    {
        if (CanToggleVideo)
        {
            VideoPaused = !VideoPaused;
        }
    }


    /// <summary>滚轮在切换条上滚动：循环切换背景（Starward <c>PointerWheelChanged</c>）。</summary>
    [RelayCommand]
    private void CycleBackground(int delta)
    {
        var count = BackgroundImages.Count;
        if (count == 0 || delta == 0)
        {
            return;
        }

        var index = (SelectedBackgroundIndex + delta) % count;
        if (index < 0)
        {
            index += count;
        }

        _ = SelectBackgroundAsync(index);
    }


    public void OnBackgroundBarPointerEnter() => ShowBackgroundBar = true;

    public void OnBackgroundBarPointerExit() => ShowBackgroundBar = false;


    /// <summary>是否正在切换背景（切换条据此保持可见，避免中途被指针离开事件收起）。</summary>
    public bool IsSwitchingBackground => Volatile.Read(ref _backgroundSwitchWorker) != 0;


    /// <summary>切换工人标记：1 表示已有一个切换循环在跑。</summary>
    private int _backgroundSwitchWorker;

    /// <summary>最近一次请求的目标下标；-1 表示没有待处理请求。</summary>
    private int _pendingBackgroundIndex = -1;


    /// <summary>
    /// 切到第 <paramref name="index"/> 个背景。
    /// </summary>
    /// <remarks>
    /// 滚轮连滚/连点圆点会反复调进来，旧实现直接 <c>_ = SelectBackgroundAsync(index)</c> 发后不管，
    /// 多个切换并发执行、互相 Stop/Start 播放器，表现就是「切一次之后开始闪、再也点不动」。
    /// 这里改成「最新目标胜出 + 单工人串行」：只保留最后一个请求，其余合并。
    /// </remarks>
    [RelayCommand]
    private async Task SelectBackgroundAsync(int index)
    {
        if (index < 0 || index >= BackgroundImages.Count)
        {
            return;
        }

        Interlocked.Exchange(ref _pendingBackgroundIndex, index);
        if (Interlocked.CompareExchange(ref _backgroundSwitchWorker, 1, 0) != 0)
        {
            // 已有工人在跑，它会把新目标消费掉。
            return;
        }

        OnPropertyChanged(nameof(IsSwitchingBackground));

        try
        {
            while (true)
            {
                var target = Interlocked.Exchange(ref _pendingBackgroundIndex, -1);
                if (target < 0)
                {
                    break;
                }

                if (target < BackgroundImages.Count)
                {
                    await ApplyBackgroundAsync(target).ConfigureAwait(true);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _backgroundSwitchWorker, 0);
            OnPropertyChanged(nameof(IsSwitchingBackground));

            // 循环退出与「又来一个新请求」之间可能擦肩而过：补一次派发，避免请求丢失。
            if (Volatile.Read(ref _pendingBackgroundIndex) >= 0)
            {
                _ = SelectBackgroundAsync(Volatile.Read(ref _pendingBackgroundIndex));
            }
        }
    }


    private async Task ApplyBackgroundAsync(int index)
    {
        if (index < 0 || index >= BackgroundImages.Count)
        {
            return;
        }

        SelectedBackgroundIndex = index;
        var item = BackgroundImages[index];

        if (item.IsVideo && _services.Background.IsVideoAvailable && _services.Config.Launcher.VideoBackground)
        {
            _context.Shell.ShowVideoBackground(item.VideoSource ?? item.Value, item.ThemeSource);
            return;
        }

        // 静态图，或视频不可用（ffmpeg 缺失/关闭动态背景）时用兜底图
        var imageSource = item.ImageSource
            ?? (item.IsVideo ? _context.Shell.Assets.For()?.Background : item.Value);
        await _context.Shell.ShowStaticBackgroundAsync(imageSource, item.ThemeSource).ConfigureAwait(true);
    }



    // ── 游戏账号切换（Starward GameAccountSwitcher） ────────────────────
    //
    // 源码是 40 高 ComboBox + 切换/编辑三块亚克力胶囊，挂在右下（Margin 0,0,40,100），
    // 由 AppConfig.EnableGameAccountSwitcher 控制。LunarWard 没有这条设置，
    // 改为"本机保存过该游戏账号就显示"，避免默认什么都不显示、用户找不到入口。

    public ObservableCollection<LauncherAccountItem> GameAccounts { get; } = [];

    [ObservableProperty]
    private LauncherAccountItem? _selectedGameAccount;

    [ObservableProperty]
    private bool _canChangeGameAccount;

    [ObservableProperty]
    private bool _isAccountSwitcherVisible;

    [ObservableProperty]
    private string _accountStatusText = "";


    partial void OnSelectedGameAccountChanged(LauncherAccountItem? value)
        => CanChangeGameAccount = value is not null;


    public void LoadGameAccounts()
    {
        GameAccounts.Clear();
        try
        {
            // 上游从 GameAccount 表读已保存账号（accounts.json 已废弃）
            foreach (var account in GameAccountStore.OpenDefault(_services.Paths.DataHome).GetAll(GameBiz))
            {
                GameAccounts.Add(new LauncherAccountItem(account));
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"读取账号列表失败：{ex.Message}");
        }

        IsAccountSwitcherVisible = _services.Config.Launcher.EnableGameAccountSwitcher
                                   && GameAccounts.Count > 0;
        SelectedGameAccount = GameAccounts.FirstOrDefault();
        CanChangeGameAccount = false;
    }


    /// <summary>把选中账号的快照写回 prefix 的 <c>user.reg</c>（Starward ChangeGameAccount）。</summary>
    [RelayCommand]
    private void ChangeGameAccount()
    {
        if (SelectedGameAccount is not { } account)
        {
            return;
        }

        try
        {
            var prefix = PrefixResolver.Resolve(
                _context.Profile.Profile.Prefix.ToDomain(), _context.Profile.Id, _services.Paths);
            var wine = new WineAccountService(prefix);
            if (wine.Write(account.Snapshot) is { } error)
            {
                AccountStatusText = error;
                return;
            }

            AccountStatusText = $"{Strings["Launcher.AccountSwitched"]}：{account.DisplayName}";
            CanChangeGameAccount = false;
        }
        catch (Exception ex)
        {
            AccountStatusText = ex.Message;
        }
    }


    [RelayCommand]
    private void OpenAccountManager()
    {
        new AccountWindow { DataContext = new AccountViewModel(_services, _context.Profile) }.Show();
        LoadGameAccounts();
    }


    // ── 每日便笺 Flyout（Starward DailyNoteButton） ────────────────────

    /// <summary>右下便笺按钮的 Flyout VM（按钮打开时刷新一次）。</summary>
    public DailyNoteButtonViewModel DailyNote { get; }


    // ── 通行证授权登录（Starward GameAuthLoginButton） ─────────────────

    /// <summary>右上角第三个 40×40 按钮：通行证 ID + 授权登录开关。</summary>
    public GameAuthLoginViewModel AuthLogin { get; }


    // ── 其他入口 ──────────────────────────────────────────────────────

    [RelayCommand]
    /// <summary>
    /// 齿轮按钮（Starward <c>StartGameButton.SettingCommand</c>）→ 弹 <c>GameLauncherSettingDialog</c>。
    /// </summary>
    private void OpenGameSettings() => SettingDialogRequested?.Invoke();

    /// <summary>
    /// 按钮下方的「定位游戏 / 配置游戏目录」小字（Starward <c>Hyperlink_LocateGame_Click</c>）：
    /// 直接开目录选择器，不弹设置对话框。
    /// </summary>
    [RelayCommand]
    private void LocateGame() => LocateGameRequested?.Invoke();

    /// <summary>
    /// 目录被外部改过（定位游戏、设置对话框的重新定位）后刷新按钮下方的两行提示与版本状态。
    /// </summary>
    public void RefreshInstallPathState()
    {
        OnPropertyChanged(nameof(ShowLocateGame));
        OnPropertyChanged(nameof(ShowNotConfigured));
        OnPropertyChanged(nameof(IsGameInstalled));
        OnPropertyChanged(nameof(ShowDx12Option));
        ApplyState(_services.LauncherService.State);
        _ = CheckUpdateAsync();

        _ = CheckDx12ConfigAsync();

    }


    /// <summary>
    /// 游戏公告：Starward 是 <c>GameNoticeWindow</c>（WebView2 + <c>GameNoticeClient.GetGameNoticeUrl</c>）。
    /// Linux 没有 WebView2，等价为用桌面浏览器打开同一个公告页（与「战绩」同策略）。
    /// </summary>
    [RelayCommand]
    private void OpenInGameNotices()
    {
        try
        {
            var url = GameNoticeClient.GetGameNoticeUrl(
                new LunarWard.Core.GameBiz(GameBiz), CurrentNoticeUid(), NoticeLanguage);
            if (SettingShellActions.TryOpen(url) is { } error)
            {
                ErrorText = error;
            }
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    private string NoticeLanguage => Strings.Language.ToLowerInvariant();


    private long CurrentNoticeUid()
        => SelectedGameAccount?.Snapshot.Uid ?? GameAccounts.FirstOrDefault()?.Snapshot.Uid ?? 0;


    /// <summary>官网页面的新公告红点（Starward <c>GameNoticeService.IsNoticeAlertAsync</c>）。</summary>
    private async Task UpdateNoticeAlertAsync()
    {
        if (_context.Services.Config.Launcher.DisableNoticeRedDot)
        {
            HasNoticeAlert = false;
            return;
        }

        try
        {
            var client = new GameNoticeClient();
            HasNoticeAlert = await client
                .IsNoticeAlertAsync(new LunarWard.Core.GameBiz(GameBiz), CurrentNoticeUid(), NoticeLanguage)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Warn($"公告红点查询失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenGalleryAsync() => await SelectBackgroundAsync(0).ConfigureAwait(true);


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _bannerTimer.Stop();
        _services.LauncherService.StateChanged -= OnStateChanged;
        _services.Installer.TaskStateChanged -= OnInstallTaskStateChanged;
        _services.LauncherService.Completed -= OnCompleted;
    }
}


public sealed class BannerViewModel(Bitmap image, string? link)
{
    public Bitmap Image { get; } = image;

    public string? Link { get; } = link;
}


public sealed partial class PostGroupViewModel(string header, IReadOnlyList<PostItemViewModel> items) : ObservableObject
{
    public string Header { get; } = header;

    public IReadOnlyList<PostItemViewModel> Items { get; } = items;


    [ObservableProperty]
    private bool _isSelected;
}


public sealed record PostItemViewModel(string Title, string Date, string Link);


/// <summary>启动器账号切换条的一项。</summary>
public sealed class LauncherAccountItem(GameAccountSnapshot snapshot)
{
    public GameAccountSnapshot Snapshot { get; } = snapshot;

    public string DisplayName => string.IsNullOrWhiteSpace(Snapshot.Name) || Snapshot.Name == "-"
        ? (Snapshot.Uid > 0 ? $"UID {Snapshot.Uid}" : Snapshot.Sha256[..8])
        : Snapshot.Name;

    public string Uid => Snapshot.Uid > 0 ? $"UID {Snapshot.Uid}" : "";
}


public sealed record BackgroundSwitchItem(
    string Title,
    string Value,
    string? ImageSource = null,
    string? VideoSource = null,
    bool IsOfficial = false,
    string? ThemeSource = null)
{
    public bool IsVideo => !string.IsNullOrWhiteSpace(VideoSource) || BackgroundCatalog.IsSupportedVideo(Value);
}


/// <summary>拉开预下载确认框所需的最小上下文（Starward 直接传 <c>CurrentGameId</c>）。</summary>
/// <param name="LauncherId">启动器 id（HoYoPlay 接口用）。</param>
/// <param name="GameBiz">游戏区服标识。</param>
/// <param name="InstallPath">已安装游戏的目录。</param>
public sealed record PredownloadDialogArgs(string LauncherId, string GameBiz, string InstallPath);


/// <summary>
/// 页面内容加载的「游戏代次」令牌：每次（重新）开始加载时递增，
/// 异步结果应用前用 <see cref="IsCurrent"/> 比对，晚到的旧请求直接丢弃。
/// </summary>
/// <remarks>
/// 切游戏时页面会重建（新的 VM + 新令牌），但旧页面的 HTTP 请求可能比新页面更晚返回；
/// 没有这个校验时旧游戏的公告会覆盖当前页。
/// </remarks>
public sealed class ContentGenerationGate
{
    private int _current;


    /// <summary>开始新一代加载，返回本代令牌。</summary>
    public int Next() => Interlocked.Increment(ref _current);


    /// <summary>该令牌是否仍是最新一代。</summary>
    public bool IsCurrent(int token) => Volatile.Read(ref _current) == token;
}
