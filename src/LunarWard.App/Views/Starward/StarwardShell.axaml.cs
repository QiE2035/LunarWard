using LunarWard.App.Services;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LunarWard.App.ViewModels;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// Starward 外壳（对应 Starward 的 <c>MainView</c>）：
/// 背景层 → 左栏导航（56px）→ 页面 Frame → 游戏选择层。
/// </summary>
public partial class StarwardShell : UserControl
{
    private readonly Dictionary<ShellPage, StarwardPage> _pages = [];
    private VideoBackgroundPlayer? _videoPlayer;

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
        if (ViewModel is { } vm)
        {
            vm.PageChanged -= OnPageChanged;
            vm.GameChanged -= OnGameChanged;
            vm.GameSelectorVisibilityChanged -= OnGameSelectorVisibilityChanged;
            vm.VideoBackgroundRequested -= OnVideoBackgroundRequested;
            vm.VideoBackgroundCleared -= OnVideoBackgroundCleared;
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
        view.OnNavigatedTo();

        if (view is Pages.ToolboxPage toolboxPage && context.Shell.PendingToolboxTab >= 0)
        {
            toolboxPage.SelectTab(context.Shell.PendingToolboxTab);
            context.Shell.PendingToolboxTab = -1;
        }
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


    private void OnGameSelectorVisibilityChanged(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm)
        {
            GameSelectorOverlayCard.IsVisible = vm.IsGameSelectorOpen;
        }
    }


    private async void OnVideoBackgroundRequested(object? sender, string path)
    {
        try
        {
            var background = ViewModel?.Services.Background;
            if (background is null || !background.IsVideoAvailable)
            {
                return;
            }

            // ffmpeg 子进程解码 → WriteableBitmap。不用 VideoView：Wayland 下没有可嵌入的
            // X11 子窗口（黑屏），而且 VLC 的编解码依赖用户装齐 vlc-plugin-* 包。
            _videoPlayer ??= new VideoBackgroundPlayer();
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
        catch (Exception ex)
        {
            ViewModel?.Log.Warn($"视频背景挂载失败：{ex.Message}");
        }
    }


    private void OnVideoBackgroundCleared(object? sender, EventArgs e)
    {
        try
        {
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
