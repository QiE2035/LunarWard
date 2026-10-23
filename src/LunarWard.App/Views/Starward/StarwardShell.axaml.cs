using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LunarWard.App.ViewModels;
using LunarWard.App.ViewModels.Starward;
using LibVLCSharp.Avalonia;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// Starward 外壳（对应 Starward 的 <c>MainView</c>）：
/// 背景层 → 左栏导航（56px）→ 页面 Frame → 游戏选择层。
/// </summary>
public partial class StarwardShell : UserControl
{
    private readonly Dictionary<ShellPage, StarwardPage> _pages = [];
    private VideoView? _videoView;

    public StarwardShell()
    {
        InitializeComponent();
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


    private void OnVideoBackgroundRequested(object? sender, string path)
    {
        try
        {
            var background = ViewModel?.Services.Background;
            if (background is null || !background.IsVlcAvailable)
            {
                return;
            }

            var player = background.EnsurePlayer(() => { });
            if (player is null)
            {
                return;
            }

            _videoView = new VideoView
            {
                MediaPlayer = player,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            };
            VideoBackgroundHost.Content = _videoView;
            background.PlayLoop(path);
        }
        catch (Exception ex)
        {
            ViewModel?.Log.Warn($"视频背景挂载失败：{ex.Message}");
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

    private void OnGameSelectorScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        ViewModel?.CloseGameSelectorCommand.Execute(null);
    }

    private async void OnGameChoiceTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is Control { DataContext: GameChoiceViewModel choice } && ViewModel is { } vm)
        {
            await Dispatcher.UIThread.InvokeAsync(async () => await vm.ChooseGameAsync(choice));
        }
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
