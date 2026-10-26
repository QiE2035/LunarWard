using System.Threading;
using System.Threading.Tasks;
using LunarWard.Services;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using LunarWard.ViewModels;
using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward;

/// <summary>
/// Starward 外壳（对应 Starward 的 <c>MainView</c>）：
/// 背景层 → 左栏导航（56px）→ 页面 Frame → 游戏选择层。
/// </summary>
public partial class StarwardShell : UserControl
{
    private static readonly TransformOperations PageEnterTransform = TransformOperations.Parse("translate(0px, 12px)");
    private static readonly TransformOperations PageRestTransform = TransformOperations.Parse("translate(0px, 0px)");

    private readonly Dictionary<ShellPage, StarwardPage> _pages = [];
    private VideoBackgroundPlayer? _videoPlayer;
    private readonly SemaphoreSlim _videoSwitchGate = new(1, 1);
    private int _videoRequestId;
    private Window? _window;
    private bool _windowActive = true;

    public StarwardShell()
    {
        InitializeComponent();
        Services.InAppToast.Initialize(ToastHost);

        if (CaptureMode.ToastTest)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Services.InAppToast.Success("已保存账号", "hk4e_cn · 100000001");
                Services.InAppToast.Warning("网络较慢", "组件索引更新用了 12s，仍可继续");
                Services.InAppToast.Error("下载失败", "raw.githubusercontent.com 连接被重置");
            }, DispatcherPriority.Background);
        }
    }

    public ShellViewModel? ViewModel => DataContext as ShellViewModel;

    private StarwardPage? _current;


    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            _window = window;
            _windowActive = window.IsActive;
            window.Activated -= OnWindowActivated;
            window.Activated += OnWindowActivated;
            window.Deactivated -= OnWindowDeactivated;
            window.Deactivated += OnWindowDeactivated;
        }

        if (ViewModel is { } vm)
        {
            vm.PageChanged -= OnPageChanged;
            vm.PageChanged += OnPageChanged;
            vm.GameChanged -= OnGameChanged;
            vm.GameChanged += OnGameChanged;
            vm.GameSelectorVisibilityChanged -= OnGameSelectorVisibilityChanged;
            vm.GameSelectorVisibilityChanged += OnGameSelectorVisibilityChanged;
            vm.VideoBackgroundRequested -= OnVideoBackgroundRequested;
            vm.VideoBackgroundRequested += OnVideoBackgroundRequested;
            vm.VideoBackgroundCleared -= OnVideoBackgroundCleared;
            vm.VideoBackgroundCleared += OnVideoBackgroundCleared;
            if (vm.VideoBackgroundPath is { } videoPath)
            {
                OnVideoBackgroundRequested(this, videoPath);
            }

            OnPageChanged(this, new PageContext
            {
                Services = vm.Services,
                Shell = vm,
                Profile = vm.Profile,
                Log = vm.Log,
            });
        }
    }


    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_window is { } window)
        {
            window.Activated -= OnWindowActivated;
            window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }

        if (ViewModel is { } vm)
        {
            vm.PageChanged -= OnPageChanged;
            vm.GameChanged -= OnGameChanged;
            vm.GameSelectorVisibilityChanged -= OnGameSelectorVisibilityChanged;
            vm.VideoBackgroundRequested -= OnVideoBackgroundRequested;
            vm.VideoBackgroundCleared -= OnVideoBackgroundCleared;
        }
    }


    private void OnWindowActivated(object? sender, EventArgs e)
    {
        _windowActive = true;
        UpdateVideoPlaybackState();
    }


    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        _windowActive = false;
        UpdateVideoPlaybackState();
    }


    /// <summary>
    /// 只有「窗口在前台 + 当前页是启动器」时才让视频背景跑：
    /// 其它时候 SIGSTOP 冻结 ffmpeg 子进程，CPU 立刻归零（用户反馈的持续占用主要来自后台播放）。
    /// </summary>
    private void UpdateVideoPlaybackState()
    {
        if (_videoPlayer is null)
        {
            return;
        }

        if (_windowActive && ViewModel?.CurrentPage == ShellPage.Launcher)
        {
            _videoPlayer.Resume();
        }
        else
        {
            _videoPlayer.Pause();
        }
    }


    private void OnPageChanged(object? sender, PageContext context)
    {
        var page = context.Shell.CurrentPage;
        if (!_pages.TryGetValue(page, out var view))
        {
            view = CreatePage(page, context);
            _pages[page] = view;
        }

        _current?.OnNavigatedFrom();
        _current = view;
        PageHost.Content = view;
        PlayPageEnter();
        view.OnNavigatedTo();

        if (view is Pages.ToolboxPage toolboxPage && context.Shell.PendingToolboxTab >= 0)
        {
            toolboxPage.SelectTab(context.Shell.PendingToolboxTab);
            context.Shell.PendingToolboxTab = -1;
        }

        UpdateVideoPlaybackState();
    }


    private void OnGameChanged(object? sender, EventArgs e)
    {
        // 换游戏 = 换 profile，所有页面都要重建（页面的 VM 缓存了 profile）
        foreach (var page in _pages.Values)
        {
            page.OnNavigatedFrom();
        }

        _pages.Clear();
        _current = null;
        PageHost.Content = null;
        if (ViewModel is { } vm)
        {
            OnPageChanged(this, new PageContext { Services = vm.Services, Shell = vm, Profile = vm.Profile, Log = vm.Log });
        }
    }


    /// <summary>
    /// 页面进场：换页时先把 <see cref="PageHost"/> 拨到「透明 + 下移 12」，下一帧再放开过渡收回原位，
    /// 得到 180ms 的淡入 + 轻微上移（上游 WinUI Frame 的默认转场观感）。
    /// </summary>
    private void PlayPageEnter()
    {
        // 写起始值时必须先摘掉 Transitions，否则会从上一页的 1/原位反向播一段淡出。
        var transitions = PageHost.Transitions;
        PageHost.Transitions = null;
        PageHost.Opacity = 0;
        PageHost.RenderTransform = PageEnterTransform;
        PageHost.Transitions = transitions;

        Dispatcher.UIThread.Post(() =>
        {
            PageHost.Opacity = 1;
            PageHost.RenderTransform = PageRestTransform;
        }, DispatcherPriority.Loaded);
    }


    private void OnGameSelectorVisibilityChanged(object? sender, EventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        if (vm.IsGameSelectorOpen)
        {
            // 先开命中再淡入：淡入期间点击能正常落到遮罩/卡片上
            GameSelectorOverlayCard.IsHitTestVisible = true;
            if (!GameSelectorContent.Classes.Contains("open"))
            {
                GameSelectorContent.Classes.Add("open");
            }

            GameSelectorOverlayCard.Opacity = 1;
        }
        else
        {
            // 先断命中再淡出：遮罩残影期间不能拦住页面点击
            GameSelectorOverlayCard.IsHitTestVisible = false;
            GameSelectorContent.Classes.Remove("open");
            GameSelectorOverlayCard.Opacity = 0;
        }
    }


    private async void OnVideoBackgroundRequested(object? sender, string path)
    {
        // 一次只跑一个切换：滚轮连滚/连点圆点会连续发请求，旧实现两次 StartAsync 互踩，
        // 表现为「切一次背景后开始闪、再也点不动」。这里用「最新请求胜出 + 串行执行」。
        var requestId = Interlocked.Increment(ref _videoRequestId);
        try
        {
            await _videoSwitchGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (requestId != Volatile.Read(ref _videoRequestId))
                {
                    return;
                }

                var background = ViewModel?.Services.Background;
                if (background is null || !background.IsVideoAvailable)
                {
                    return;
                }

                // 官方背景视频先落本地缓存再播（上游 BackgroundService 同款）：否则
                // `-stream_loop -1` 每循环一圈都会重新拉一遍整段视频，带宽和首帧都遭殃。
                var localPath = await background.EnsureVideoCachedAsync(path).ConfigureAwait(true);
                if (requestId != Volatile.Read(ref _videoRequestId))
                {
                    return;
                }

                if (localPath is { Length: > 0 })
                {
                    path = localPath;
                }

                // ffmpeg 子进程解码 → WriteableBitmap。不用 VideoView：Wayland 下没有可嵌入的
                // X11 子窗口（黑屏），而且 VLC 的编解码依赖用户装齐 vlc-plugin-* 包。
                _videoPlayer ??= new VideoBackgroundPlayer();
                await _videoPlayer.StopAsync().ConfigureAwait(true);
                background.AttachPlayer(_videoPlayer);
                VideoBackgroundHost.Content = _videoPlayer.View;
                if (!await _videoPlayer.StartAsync(path, ViewModel?.Log).ConfigureAwait(true))
                {
                    // 拿不到帧就回退官方静态图，别留黑屏。
                    _videoPlayer.Stop();
                    VideoBackgroundHost.Content = null;
                    ViewModel?.Log.Warn($"动态背景无法播放，回退静态图：{path}");
                    if (ViewModel is { } vm)
                    {
                        await vm.ShowStaticBackgroundAsync(vm.Assets.For()?.Background).ConfigureAwait(true);
                    }
                }
            }
            finally
            {
                _videoSwitchGate.Release();
            }
        }
        catch (Exception ex)
        {
            ViewModel?.Log.Warn($"视频背景挂载失败：{ex.Message}");
        }

        UpdateVideoPlaybackState();
    }


    private void OnVideoBackgroundCleared(object? sender, EventArgs e)
    {
        try
        {
            // 取消可能在排队的播放请求：清空背景后不应再被旧请求重新挂上。
            Interlocked.Increment(ref _videoRequestId);
            VideoBackgroundHost.Content = null;
            _videoPlayer?.Dispose();
            _videoPlayer = null;
            ViewModel?.Services.Background.AttachPlayer(null);
            ViewModel?.Services.Background.Stop();
        }
        catch (Exception ex)
        {
            ViewModel?.Log.Warn($"视频背景卸载失败：{ex.Message}");
        }
    }


    /// <summary>自检辅助：把工具箱切到指定 Tab（0 = 每日便笺，1 = 账号记录）。</summary>
    public void SelectToolboxTab(int index)
    {
        if (_current is Pages.ToolboxPage toolbox)
        {
            toolbox.SelectTab(index);
        }
    }


    private static StarwardPage CreatePage(ShellPage page, PageContext context) => page switch
    {
        ShellPage.Launcher => new Pages.GameLauncherPage(context),
        ShellPage.GameSetting => new Pages.GameSettingPage(context),
        ShellPage.Screenshot => new Pages.ScreenshotPage(context),
        ShellPage.Gacha => new Pages.GachaPage(context),
        ShellPage.Toolbox => new Pages.ToolboxPage(context),
        ShellPage.SelfQuery => new Pages.SelfQueryPage(context),

        ShellPage.GenshinBeyondGacha => new Pages.GenshinBeyondGachaPage(context),
        ShellPage.Settings => new Pages.SettingPage(context),
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };


    // ── 标题栏 ───────────────────────────────────────────────────────

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && TopLevel.GetTopLevel(this) is Window window)
        {
            window.BeginMoveDrag(e);
        }
    }


    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        // Starward 主窗口不可最大化，这里保持同样行为：点击不动作，只给视觉反馈
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close();
        }
    }


    // ── 游戏选择层 ───────────────────────────────────────────────────

    private void OnGameIconsAreaPointerExited(object? sender, PointerEventArgs e)
        => ViewModel?.HideGameIcons();


    private void OnGameSelectorScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        ViewModel?.CloseGameSelectorCommand.Execute(null);
    }

    private Avalonia.Point _dragStart;
    private GameChoiceViewModel? _dragChoice;
    private PointerPressedEventArgs? _dragPress;


    /// <summary>按下记录起点：拖动超过阈值才算排序（源码「长按/拖动」交互的等价物）。</summary>
    private void OnGameChoicePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: GameChoiceViewModel choice })
        {
            _dragStart = e.GetPosition(this);
            _dragChoice = choice;
            _dragPress = e;
        }
    }


    private async void OnGameChoicePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragChoice is null || _dragPress is null || sender is not Control card || !e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var now = e.GetPosition(this);
        if (Math.Abs(now.X - _dragStart.X) < 8 && Math.Abs(now.Y - _dragStart.Y) < 8)
        {
            return;
        }

        var choice = _dragChoice;
        _dragChoice = null;
        var press = _dragPress;
        _dragPress = null;
        var format = DataFormat.CreateStringApplicationFormat("lunarward/game-choice");
        var item = new DataTransferItem();
        item.Set(format, choice.Kind);
        var data = new DataTransfer();
        data.Add(item);
        await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Move);
    }


    /// <summary>松开即清拖动状态（否则点击后残留起点会让下一次微动触发拖放）。</summary>
    private void OnGameChoicePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragChoice = null;
        _dragPress = null;
    }


    private void OnGameChoiceDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.CreateStringApplicationFormat("lunarward/game-choice")) ? DragDropEffects.Move : DragDropEffects.None;
    }


    private void OnGameChoiceDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control card || card.DataContext is not GameChoiceViewModel target
            || DataContext is not ShellViewModel shell)
        {
            return;
        }

        if (e.DataTransfer.TryGetValue(DataFormat.CreateStringApplicationFormat("lunarward/game-choice")) is { } sourceKind)
        {
            shell.MoveGameChoice(sourceKind, shell.GameChoices.IndexOf(target));
        }
    }


    /// <summary>点区服行：切档案并写渠道（源码 <c>Button_GameServer_Click</c>）。</summary>
    private void OnGameServerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: GameServerItem server } && DataContext is ShellViewModel shell)
        {
            shell.SelectServerCommand.Execute(server);
        }
    }


    /// <summary>「固定」按钮（源码 <c>Button_PinGameBiz_Click</c>）。</summary>
    private void OnGameServerPinClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Control { DataContext: GameServerItem server } && DataContext is ShellViewModel shell)
        {
            shell.ToggleServerPinCommand.Execute(server);
        }
    }


    /// <summary>
    /// 点卡片：单区服直接切；多区服在卡片下方弹区服 flyout
    /// （源码 <c>Grid_GameBizDisplay_Tapped</c>，Placement=Bottom / Transient）。
    /// hover **不** 弹 flyout——那是之前闪烁的根因，hover 只做高亮（XAML 里的 choiceHover）。
    /// </summary>
    private void OnGameChoiceTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is not Control { DataContext: GameChoiceViewModel choice } card || ViewModel is not { } vm)
        {
            return;
        }

        e.Handled = true;

        if (vm.ChooseGame(choice))
        {
            return;
        }

        if (card.ContextFlyout is PopupFlyoutBase flyout)
        {
            flyout.Placement = PlacementMode.Bottom;
            flyout.ShowMode = FlyoutShowMode.Transient;
            flyout.ShowAt(card);
        }
    }


    /// <summary>左上角当前游戏图标：指针进入滑出图标条（源码 <c>Border_CurrentGameIcon_PointerEntered</c>）。</summary>
    private void OnCurrentGameIconPointerEntered(object? sender, PointerEventArgs e)
        => ViewModel?.ShowGameIcons();


    /// <summary>
    /// 指针离开当前游戏图标：向右移入图标条时不收（源码 <c>Border_CurrentGameIcon_PointerExited</c>
    /// 的 <c>position.X &gt; ActualSize.X - 1</c> 判断），其余情况收起。
    /// </summary>
    private void OnCurrentGameIconPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control icon && e.GetPosition(icon).X > icon.Bounds.Width - 1)
        {
            return;
        }

        ViewModel?.HideGameIcons();
    }


    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ViewModel?.IsGameSelectorOpen == true)
        {
            ViewModel.CloseGameSelectorCommand.Execute(null);
            e.Handled = true;
        }
    }
}
