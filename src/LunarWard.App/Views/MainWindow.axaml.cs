using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LunarWard.App.Services;
using LunarWard.App.ViewModels;
using LibVLCSharp.Avalonia;

namespace LunarWard.App.Views;

/// <summary>
/// 主窗口。视图层只负责：Acrylic 分级、背景视频/图片挂载、主题样式切换。
/// </summary>
public partial class MainWindow : Window
{
    private VideoView? _videoView;

    private Image? _imageView;

    private AcrylicLevelKind _level = AcrylicLevelKind.Translucent;


    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }


    private void OnOpened(object? sender, EventArgs e)
    {
        _level = AcrylicLevel.Detect(this);
        ApplyAcrylic();
        ApplyThemeStyle();
        MountBackground();
        ViewModel?.RefreshProfileDetailsIfSelected();

        if (CaptureMode.Enabled)
        {
            CaptureMode.Capture(this);
        }
    }


    private void OnClosed(object? sender, EventArgs e)
    {
        if (_videoView is not null)
        {
            BackgroundHost.Child = null;
            _videoView = null;
        }
    }


    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;


    /// <summary>
    /// L2：半透明渐变；L3：纯色。窗口本身允许透明（TransparencyLevelHint），
    /// 合成器不支持时 Avalonia 会自动回落到不透明背景。
    /// </summary>
    private void ApplyAcrylic()
    {
        AcrylicLayer.Background = AcrylicLevel.BrushFor(_level);
        TransparencyBackgroundFallback = AcrylicLevel.SolidBrush;

        if (_level == AcrylicLevelKind.Solid)
        {
            Background = AcrylicLevel.SolidBrush;
        }
    }


    /// <summary>Modern（卡片）/ Classic（全屏背景 + 悬浮控件）。</summary>
    private void ApplyThemeStyle()
    {
        var style = ViewModel?.SelectedTheme ?? "Modern";
        Classes.Set("classic", style == "Classic");
        Classes.Set("modern", style != "Classic");
    }


    private void MountBackground()
    {
        var background = ViewModel?.BackgroundService;
        if (background is null)
        {
            return;
        }

        var items = background.Discover();
        var first = items.FirstOrDefault(x => x.IsVideo);
        if (first is not null)
        {
            var player = background.EnsurePlayer(() => { });
            if (player is not null)
            {
                _videoView = new VideoView
                {
                    MediaPlayer = player,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                };
                BackgroundHost.Child = _videoView;
                background.PlayLoop(first.VideoPath!);
                return;
            }
        }

        var image = items.FirstOrDefault(x => x.ImagePath is not null);
        if (image is not null)
        {
            try
            {
                _imageView = new Image
                {
                    Source = new Bitmap(image.ImagePath!),
                    Stretch = Stretch.UniformToFill,
                };
                BackgroundHost.Child = _imageView;
            }
            catch (Exception)
            {
                // 坏图静默跳过
            }
        }
    }
}
