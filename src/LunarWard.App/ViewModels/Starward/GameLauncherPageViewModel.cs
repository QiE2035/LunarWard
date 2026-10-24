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
using LunarWard.Core;
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

        _ = LoadContentAsync();
        _ = UpdateNoticeAlertAsync();
        RefreshBackgroundImages();
        LoadGameAccounts();
    }


    // ── 游戏 ──────────────────────────────────────────────────────────

    public string GameBiz { get; }

    /// <summary>XAML 里的文案索引器（<c>{Binding Strings[Launcher.StartGame]}</c>）。</summary>
    public Localization.AppStrings Strings => Localization.AppStrings.Instance;

    public string GameName => _context.Profile.DisplayName;

    public bool IsGenshin => GameBiz.StartsWith("hk4e", StringComparison.Ordinal);

    public bool SupportsDailyNote => true;

    public bool SupportsGacha => GameBiz.StartsWith("hk4e", StringComparison.Ordinal)
                                 || GameBiz.StartsWith("nap", StringComparison.Ordinal);


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
    private void NextBanner()
    {
        if (Banners.Count == 0)
        {
            return;
        }

        CurrentBannerIndex = (CurrentBannerIndex + 1) % Banners.Count;
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
        ContentStatus = Strings["Launcher.LoadingContent"];
        try
        {
            var assets = _context.Shell.Assets;
            await assets.LoadAsync(_context.Shell.CurrentLauncherId, GameBiz).ConfigureAwait(true);
            var content = assets.Content;
            if (content is null)
            {
                HasContent = false;
                ContentStatus = Strings["Launcher.OfflineContent"];
                return;
            }

            foreach (var banner in content.Banners ?? [])
            {
                var bitmap = await assets.BitmapAsync(banner.Image?.Url).ConfigureAwait(true);
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
                BannerCounterText = $"1/{Banners.Count}";
                _bannerTimer.Start();
            }

            HasContent = Banners.Count > 0 || Posts.Any(x => x.Items.Count > 0);
            ContentStatus = HasContent ? "" : Strings["Launcher.NoContent"];
        }
        catch (Exception ex)
        {
            _log.Warn($"启动器页资讯加载失败：{ex.Message}");
            HasContent = false;
            ContentStatus = Strings["Launcher.ContentFailed"];
        }
        finally
        {
            OnPropertyChanged(nameof(CurrentPivotHeader));
        }
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
    [NotifyPropertyChangedFor(nameof(IsAccentBackground), nameof(StartButtonForeground), nameof(SettingButtonForeground))]
    private bool _isRunning;

    /// <summary>
    /// Starward <c>StartGameButton</c>：可点且不是安装中 → 强调色底 + 强调色上的文字
    /// （见 <c>IsAccentColorBackgroundVisible</c> / <c>ActionButtonForeground</c>）。
    /// </summary>
    public bool IsAccentBackground => IsStartButtonEnabled && !IsRunning && !IsBusy;

    public Avalonia.Media.IBrush StartButtonForeground => IsAccentBackground
        ? Avalonia.Media.Brushes.Black
        : Avalonia.Media.Brush.Parse("#FFFFFFFF");

    public Avalonia.Media.IBrush SettingButtonForeground => IsAccentBackground
        ? Avalonia.Media.Brushes.Black
        : Avalonia.Media.Brush.Parse("#FFFFFFFF");


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
    public bool ShowDx12Option => !ShowNotConfigured;


    public bool ShowNotConfigured => string.IsNullOrWhiteSpace(
        ProfileConfig.ExpandPath(_context.Profile.Profile.Path));


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
        // Starward：GameState.InstallGame 时按钮是「安装游戏」，点击打开安装对话框而不是启动游戏。
        if (!IsGameInstalled && !IsInstalling)
        {
            InstallDialogRequested?.Invoke();
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
            LaunchPhaseState.Idle or LaunchPhaseState.Error => Strings["Launcher.StartGame"],
            LaunchPhaseState.Running => Strings["Launcher.GameRunning"],
            _ => text,
        };
        IsStartButtonEnabled = enabled || (state is LaunchPhaseState.Idle or LaunchPhaseState.Error && !IsGameInstalled);
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


    // ── 安装 / 更新（Starward StartGameButton 的安装进度弹层）────────────

    /// <summary>视图订阅它来弹「选择安装路径」对话框（Starward 在页面代码里 ShowAsync）。</summary>
    public event Action? InstallDialogRequested;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string _installStatusText = "";

    [ObservableProperty]
    private string _installPercentText = "";

    [ObservableProperty]
    private string _installDetailText = "";

    [ObservableProperty]
    private bool _installCanResume;

    private GameInstallRequest? _installRequest;
    private GameInstallState _installState = GameInstallState.Stop;


    /// <summary>游戏是否已经装好（目录里能找到 exe）。</summary>
    public bool IsGameInstalled => !ShowNotConfigured && !ShowLocateGame;


    /// <summary>自检截图专用：显示一张假的安装进度卡（不触发真实网络）。</summary>
    public void ShowInstallProgressForCapture()
    {
        IsInstalling = true;
        InstallCanResume = false;
        InstallStatusText = Strings["InstallGameDialog.Installing"];
        InstallProgress = 46.4;
        InstallPercentText = "46.4%";
        InstallDetailText = "4.32 GB / 9.31 GB  18.2 MB/s";
    }


    /// <summary>由页面代码在对话框确认后调用：开始下载安装。</summary>
    public async Task BeginInstallAsync(GameInstallRequest request)
    {
        _installRequest = request;
        IsInstalling = true;
        InstallCanResume = false;
        InstallProgress = 0;
        InstallStatusText = Strings["InstallGameDialog.Installing"];
        InstallPercentText = "0%";
        InstallDetailText = "";

        var progress = _services.Installer.StartOrContinue(request);
        ApplyInstallProgress(progress);
        var task = _services.Installer.CurrentTask;
        if (task is not null)
        {
            // 安装器内部是同步长任务（下载/解压/校验），放到线程池跑，UI 靠 Progress 回调刷新。
            await Task.Run(() => _services.Installer.RunAsync(task)).ConfigureAwait(true);
        }
    }


    [RelayCommand]
    private void PauseInstall()
    {
        if (_installRequest is null)
        {
            return;
        }

        ApplyInstallProgress(_services.Installer.Pause(_installRequest));
    }


    [RelayCommand]
    private void ResumeInstall()
    {
        if (_installRequest is null)
        {
            return;
        }

        var progress = _services.Installer.StartOrContinue(_installRequest);
        ApplyInstallProgress(progress);
        if (_services.Installer.CurrentTask is { } task)
        {
            _ = Task.Run(() => _services.Installer.RunAsync(task));
        }
    }


    [RelayCommand]
    private void CancelInstall()
    {
        if (_installRequest is null)
        {
            return;
        }

        _services.Installer.Stop(_installRequest);
        IsInstalling = false;
        InstallCanResume = false;
        InstallStatusText = "";
        InstallDetailText = "";
        InstallPercentText = "";
        InstallProgress = 0;
        _installRequest = null;
        ApplyState(_services.LauncherService.State);
    }


    private void OnInstallTaskStateChanged(object? sender, GameInstallContext context)
        => Dispatcher.UIThread.Post(() => ApplyInstallProgress(GameInstallProgress.FromTask(context)));


    /// <summary>把安装器快照翻成界面文案（Starward 弹层里的百分比/速度/剩余时间）。</summary>
    private void ApplyInstallProgress(GameInstallProgress progress)
    {
        _installState = (GameInstallState)progress.State;
        InstallProgress = Math.Clamp(progress.ProgressPercent, 0, 100);
        InstallPercentText = $"{progress.ProgressPercent:F1}%";
        InstallCanResume = _installState is GameInstallState.Paused or GameInstallState.Error;

        InstallStatusText = _installState switch
        {
            GameInstallState.Waiting => Strings["InstallGameDialog.Installing"],
            GameInstallState.Downloading => Strings["InstallGameDialog.Downloading"],
            GameInstallState.Paused => Strings["InstallGameDialog.Paused"],
            GameInstallState.Finish => Strings["InstallGameDialog.Completed"],
            GameInstallState.Error => string.Format(Strings["InstallGameDialog.Failed"], progress.ErrorMessage ?? ""),
            _ => Strings["InstallGameDialog.Installing"],
        };

        if (_installState is GameInstallState.Downloading or GameInstallState.Waiting)
        {
            var finish = FormatBytes(progress.ProgressDownloadFinishBytes);
            var total = progress.ProgressDownloadTotalBytes > 0 ? FormatBytes(progress.ProgressDownloadTotalBytes) : "?";
            var speed = progress.NetworkDownloadSpeed > 0 ? $"{FormatBytes((long)progress.NetworkDownloadSpeed)}/s" : "";
            InstallDetailText = $"{finish} / {total}  {speed}".Trim();
        }

        if (_installState is GameInstallState.Finish)
        {
            IsInstalling = false;
            _installRequest = null;
            InstallDetailText = "";
            OnInstallFinished(progress);
        }
        else if (_installState is GameInstallState.Stop)
        {
            IsInstalling = false;
            _installRequest = null;
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

        OnPropertyChanged(nameof(IsGameInstalled));
        OnPropertyChanged(nameof(ShowNotConfigured));
        OnPropertyChanged(nameof(ShowLocateGame));
        ApplyState(_services.LauncherService.State);
        StatusText = Strings["InstallGameDialog.Completed"];
        _log.Info($"安装完成：{progress.GameBiz} → {progress.InstallPath}");
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


    public void UpdatePlayTime()
    {
        var total = _services.PlayTime.TotalFor(_context.Profile.Id);
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


    public void RefreshBackgroundImages()
    {
        BackgroundImages.Clear();
        var art = _context.Shell.Assets.For();
        if (art?.Background is { } official)
        {
            BackgroundImages.Add(new BackgroundSwitchItem("官方背景", official));
        }

        foreach (var item in _services.Background.Discover())
        {
            BackgroundImages.Add(new BackgroundSwitchItem(item.Title ?? Path.GetFileName(item.ImagePath ?? item.VideoPath ?? ""),
                item.ImagePath ?? item.VideoPath ?? ""));
        }

        HasVideoBackground = BackgroundImages.Any(x => x.IsVideo);
        SelectedBackgroundIndex = 0;
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


    [RelayCommand]
    private async Task SelectBackgroundAsync(int index)
    {
        if (index < 0 || index >= BackgroundImages.Count)
        {
            return;
        }

        SelectedBackgroundIndex = index;
        var item = BackgroundImages[index];
        if (item.IsVideo)
        {
            BackgroundRequested?.Invoke(this, item.Value);
            return;
        }

        if (await _context.Shell.Assets.BitmapAsync(item.Value).ConfigureAwait(true) is { } bitmap)
        {
            _context.Shell.BackgroundImage = bitmap;
        }
    }


    /// <summary>视图挂载视频背景。</summary>
    public event EventHandler<string>? BackgroundRequested;


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
            var path = Path.Join(_services.Paths.DataHome, "accounts.json");
            if (File.Exists(path))
            {
                var accounts = JsonSerializer.Deserialize<List<GameAccountSnapshot>>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                foreach (var account in accounts.Where(x => x.GameBiz == GameBiz))
                {
                    GameAccounts.Add(new LauncherAccountItem(account));
                }
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


    // ── 其他入口 ──────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenGameSettings() => _context.Shell.Navigate(ShellPage.GameSetting);


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


public sealed record BackgroundSwitchItem(string Title, string Value)
{
    public bool IsVideo => Value.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                           || Value.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
                           || Value.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase);
}
