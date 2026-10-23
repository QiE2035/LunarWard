using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

public partial class GameLauncherPage : StarwardPage
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    private GameLauncherPageViewModel? _viewModel;


    public GameLauncherPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new GameLauncherPageViewModel(context);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }


    public override void OnNavigatedTo()
    {
        _clock.Tick += OnClockTick;
        _clock.Start();

        // 自检：--open-daily-note 时在页面挂好后展开便笺 Flyout。
        if (CaptureMode.OpenDailyNote)
        {
            DispatcherTimer.RunOnce(
                () => Button_DailyNote.Flyout?.ShowAt(Button_DailyNote),
                TimeSpan.FromMilliseconds(600));
        }
    }


    public override void OnNavigatedFrom()
    {
        _clock.Stop();
        _clock.Tick -= OnClockTick;
        base.OnNavigatedFrom();
    }


    private void OnClockTick(object? sender, EventArgs e) => _viewModel?.TickRunningTime();


    private void OnBannerPointerEntered(object? sender, PointerEventArgs e) => _viewModel?.OnBannerPointerEnter();


    private void OnBannerPointerExited(object? sender, PointerEventArgs e) => _viewModel?.OnBannerPointerExit();


    private void OnBannerTapped(object? sender, TappedEventArgs e) => _viewModel?.OpenBannerLinkCommand.Execute(null);


    private void OnBackgroundBarPointerEntered(object? sender, PointerEventArgs e) => _viewModel?.OnBackgroundBarPointerEnter();


    private void OnBackgroundBarPointerExited(object? sender, PointerEventArgs e) => _viewModel?.OnBackgroundBarPointerExit();


    /// <summary>便笺 Flyout 打开：绑定 Flyout VM 并刷新一次（Starward 的按钮 Loaded 时拉取）。</summary>
    private void OnDailyNoteFlyoutOpened(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        DailyNoteFlyoutRoot.DataContext = _viewModel.DailyNote;
        _viewModel.DailyNote.RefreshCommand.Execute(null);

        // Flyout 是独立 Popup，主窗口的 RenderTargetBitmap 拍不到；自检时单独把 PopupRoot 存一张。
        if (CaptureMode.Enabled)
        {
            DispatcherTimer.RunOnce(CaptureFlyout, TimeSpan.FromMilliseconds(800));
        }
    }


    private void CaptureFlyout()
    {
        try
        {
            if (TopLevel.GetTopLevel(DailyNoteFlyoutRoot) is not { } root)
            {
                Console.WriteLine("FLYOUT_SHOT_FAIL no popup root");
                return;
            }

            var size = new PixelSize(
                Math.Max(1, (int)root.Bounds.Width),
                Math.Max(1, (int)root.Bounds.Height));
            using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
            bitmap.Render(root);
            var path = CaptureMode.Path + ".flyout.png";
            using (var stream = File.Create(path))
            {
                bitmap.Save(stream, new PngBitmapEncoderOptions());
            }

            Console.WriteLine($"SCREENSHOT_OK {path} {size.Width}x{size.Height}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FLYOUT_SHOT_FAIL {ex.GetType().Name}: {ex.Message}");
        }
    }
}
