using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.App.ViewModels;
using LunarWard.App.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
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

        ApplyState(_services.LauncherService.State);
        UpdatePlayTime();
        _ = LoadContentAsync();
        RefreshBackgroundImages();
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


    public string CurrentPivotHeader => Posts.FirstOrDefault()?.Header ?? "活动";


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
        ContentStatus = "正在获取游戏资讯…";
        try
        {
            var assets = _context.Shell.Assets;
            await assets.LoadAsync(_context.Shell.CurrentLauncherId, GameBiz).ConfigureAwait(true);
            var content = assets.Content;
            if (content is null)
            {
                HasContent = false;
                ContentStatus = "离线：没有拿到活动/公告（不影响启动）";
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
                ("活动", GamePostType.POST_TYPE_ACTIVITY),
                ("公告", GamePostType.POST_TYPE_ANNOUNCE),
                ("资讯", GamePostType.POST_TYPE_INFO),
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
            ContentStatus = HasContent ? "" : "暂无活动与公告";
        }
        catch (Exception ex)
        {
            _log.Warn($"启动器页资讯加载失败：{ex.Message}");
            HasContent = false;
            ContentStatus = "拉取资讯失败（离线或接口不可用）";
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
        var launcher = _services.LauncherService;
        if (launcher.State == LaunchPhaseState.Running)
        {
            launcher.RequestStop();
            StatusText = "正在停止…";
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
                ? (result.Signal is { } signal ? $"游戏已退出（就绪信号：{signal}）" : "游戏已退出")
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
            LaunchPhaseState.Idle or LaunchPhaseState.Error => Strings["Launcher.StartGame"],
            LaunchPhaseState.Running => Strings["Launcher.GameRunning"],
            _ => text,
        };
        IsStartButtonEnabled = enabled;
        IsBusy = state is LaunchPhaseState.Prepare or LaunchPhaseState.Launch
            or LaunchPhaseState.AwaitImage or LaunchPhaseState.AwaitReady or LaunchPhaseState.Cleanup;
        IsRunning = state == LaunchPhaseState.Running;
        StatusText = state switch
        {
            LaunchPhaseState.Idle => "就绪",
            LaunchPhaseState.Running => "游戏运行中",
            LaunchPhaseState.Error => "出错了",
            _ => text,
        };

        if (state == LaunchPhaseState.Running)
        {
            _runningSince ??= DateTimeOffset.Now;
            RunningInfo = _services.LauncherService.GamePid is { } pid ? $"PID {pid}" : "游戏运行中";
            _services.Background.Pause();
        }
        else if (state == LaunchPhaseState.Idle)
        {
            _runningSince = null;
            RunningInfo = "";
            _services.Background.Resume();
        }
    }


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


    // ── 其他入口 ──────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenGameSettings() => _context.Shell.Navigate(ShellPage.GameSetting);

    /// <summary>
    /// Starward 的便笺按钮是浮层；这里统一跳到工具箱的「每日便笺」Tab——
    /// 同一份功能只保留一个界面，避免两套视图各改一半。
    /// </summary>
    [RelayCommand]
    private void OpenDailyNote() => _context.Shell.NavigateToToolbox(0);

    /// <summary>账号切换同样落到工具箱的「账号记录」Tab。</summary>
    [RelayCommand]
    private void OpenAccounts() => _context.Shell.NavigateToToolbox(1);


    [RelayCommand]
    private void OpenInGameNotices()
    {
        if (Banners.Count == 0)
        {
            return;
        }

        _ = OpenBannerLinkAsync();
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


public sealed record BackgroundSwitchItem(string Title, string Value)
{
    public bool IsVideo => Value.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                           || Value.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
                           || Value.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase);
}
