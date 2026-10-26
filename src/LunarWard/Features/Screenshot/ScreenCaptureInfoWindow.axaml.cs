using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using LunarWard.Language;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 截图后的浮层信息窗（上游 <c>Features/Screenshot/ScreenCaptureInfoWindow.xaml(.cs)</c>）。
/// <para>
/// Linux 差异（docs/09 §8.3）：WinUI 的 <c>AppWindow.MoveAndResize</c>/<c>AppWindow.Show(false)</c>（不激活显示）
/// 在 Avalonia 用 <see cref="Window.ShowActivated"/>=false + <see cref="Window.Position"/> 实现；
/// 合成器 <c>Vector3KeyFrameAnimation</c> 换成 <see cref="TranslateTransform"/> + <see cref="Transitions"/>；
/// 游戏客户区位置用 <c>xdotool getwindowgeometry --shell</c> 查询（Wayland 下拿不到则退回屏幕右缘）；
/// Win2D 的缩略图裁剪（CropEffect/ScaleEffect/HDR tone map）换成 <see cref="Bitmap"/> 的直接缩放绘制。
/// </para>
/// </summary>
public sealed partial class ScreenCaptureInfoWindow : Window
{

    private const int WindowWidth = 320;

    private const int WindowHeight = 100;

    private readonly ILogger<ScreenCaptureInfoWindow> _logger = AppConfig.GetLogger<ScreenCaptureInfoWindow>();


    public ScreenCaptureInfoWindow()
    {
        InitializeComponent();
        DataContext = this;
        InitializeWindow();
        this.Closed += ScreenCaptureInfoWindow_Closed;
    }


    public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;


    private bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));
        return true;
    }


    private void InitializeWindow()
    {
        // 上游：AppWindow.TitleBar.ExtendsContentIntoTitleBar = true; IsShownInSwitchers = false;
        //       OverlappedPresenter 关边框/不可缩放/AlwaysOnTop。
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
    }


    private void ScreenCaptureInfoWindow_Closed(object? sender, EventArgs e)
    {
        this.Closed -= ScreenCaptureInfoWindow_Closed;
        Button_OpenImage.Click -= Button_OpenImage_Click;
        Button_OpenLog.Click -= Button_OpenLog_Click;
        _imageViewWindow2 = null;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }


    public bool IsSuccess { get; set => SetProperty(ref field, value); }

    public bool IsError { get; set => SetProperty(ref field, value); }



    private int _captureImageCount;

    private int _finishedImageCount;

    private string? _lastFile;

    private CancellationTokenSource? _cancellationTokenSource;

    private CancellationToken _openImageCancellationToken;

    private Bitmap? _thumbnail;

    private ImageViewWindow2? _imageViewWindow2;

    private TranslateTransform? _translateTransform;

    private long _lastShowAnimationTs;



    /// <summary>
    /// 开始截图后调用此方法显示信息
    /// </summary>
    public void CaptureStart(nint hwnd, Bitmap bitmap, float maxCLL = -1)
    {
        IsSuccess = true;
        IsError = false;
        _captureImageCount++;
        CropImage(bitmap);
        _cancellationTokenSource?.Cancel();
        DisplayWindow(hwnd, true);
    }


    /// <summary>
    /// 截图成功后调用此方法来显示截图信息窗口
    /// </summary>
    public void CaptureSuccess(nint hwnd, Bitmap bitmap, string file, float maxCLL = -1)
    {
        try
        {
            if (IsError)
            {
                CropImage(bitmap);
            }
            IsSuccess = true;
            IsError = false;
            _lastFile = file;
            _finishedImageCount++;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _openImageCancellationToken = _cancellationTokenSource.Token;
            DisplayWindow(hwnd, false, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CaptureSuccess");
        }
    }


    /// <summary>
    /// 剪裁并缩放至正方形大小（上游用 Win2D CropEffect + ScaleEffect + HDR tone map，这里直接缩放源图居中裁剪）
    /// </summary>
    private void CropImage(Bitmap bitmap)
    {
        try
        {
            int size = (int)Math.Round(72 * (RenderScaling <= 0 ? 1 : RenderScaling));
            PixelSize sourceSize = bitmap.PixelSize;
            int cropSize = Math.Min(sourceSize.Width, sourceSize.Height);
            int cropX = (sourceSize.Width - cropSize) / 2;
            int cropY = (sourceSize.Height - cropSize) / 2;
            // Avalonia 的 CroppedBitmap 不是 Bitmap 子类，没有 CreateScaledBitmap；用 RenderTargetBitmap 等比绘制。
            var cropped = new CroppedBitmap(bitmap, new PixelRect(cropX, cropY, cropSize, cropSize));
            // HDR（R16G16B16A16Float）上游会 tone map 到 SDR，这里 Bitmap 解码后已是 SDR。
            var scaled = new RenderTargetBitmap(new PixelSize(size, size));
            using (var context = scaled.CreateDrawingContext())
            {
                context.DrawImage(cropped, new Rect(0, 0, cropSize, cropSize), new Rect(0, 0, size, size));
            }
            _thumbnail?.Dispose();
            _thumbnail = scaled;
            ThumbnailImage.Source = _thumbnail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CropImage");
        }
    }


    /// <summary>
    /// 显示错误信息
    /// </summary>
    public void CaptureError(nint hwnd, bool captureStarted)
    {
        try
        {
            IsError = true;
            IsSuccess = false;
            if (captureStarted)
            {
                _finishedImageCount++;
            }
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _openImageCancellationToken = _cancellationTokenSource.Token;
            DisplayWindow(hwnd, false, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CaptureError");
        }
    }



    private async void DisplayWindow(nint hwnd, bool doNotClose = false, CancellationToken cancellationToken = default)
    {
        try
        {
            double scaling = RenderScaling <= 0 ? 1 : RenderScaling;
            int width = (int)(WindowWidth * scaling);
            int height = (int)(WindowHeight * scaling);

            (int clientX, int clientY, int clientWidth, int clientHeight) = GetClientBounds(hwnd);

            int targetX = clientX + clientWidth - width;
            int targetY = clientY + (int)(clientHeight * 0.25) - height / 2;

            var area = GetScreenWorkArea(clientX + clientWidth / 2, clientY + clientHeight / 2);

            bool exceedsClient = targetX < clientX
                                 || targetX + width > clientX + clientWidth
                                 || targetY < clientY
                                 || targetY + height > clientY + clientHeight;

            bool exceedsScreen = targetX < area.X
                                 || targetX + width > area.Right
                                 || targetY < area.Y
                                 || targetY + height > area.Bottom;

            if (exceedsClient || exceedsScreen)
            {
                targetX = area.Right - width;
                targetY = area.Y + (int)(area.Height * 0.25) - height / 2;
            }

            bool complete = _finishedImageCount == _captureImageCount;
            TextBlock_State.Text = complete ? Lang.ScreenCaptureInfoWindow_ScreenshotSaved : Lang.ScreenCaptureInfoWindow_ProcessingImage;
            ProgressRing_Process.IsVisible = !complete;
            FontIcon_Complete.IsVisible = complete;
            if (_captureImageCount > 1)
            {
                TextBlock_Repeat.IsVisible = true;
                TextBlock_Repeat.Text = $"{_finishedImageCount}/{_captureImageCount}";
            }
            Button_OpenImage.IsVisible = IsSuccess && _finishedImageCount > 0;

            Width = WindowWidth;
            Height = WindowHeight;
            Position = new PixelPoint(targetX, targetY);
            Show();
            StartShowAnimation();
            if (doNotClose)
            {
                return;
            }
            await Task.Delay(2000, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                await HideWindowAsync(cancellationToken);
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to display screenshot info window.");
        }
    }


    private async Task HideWindowAsync(CancellationToken cancellationToken)
    {
        if (_finishedImageCount < _captureImageCount)
        {
            return;
        }
        StartHideAnimation();
        await Task.Delay(600, cancellationToken);
        Hide();
        _captureImageCount = 0;
        _finishedImageCount = 0;
        _lastFile = null;
        TextBlock_Repeat.Text = "";
        TextBlock_Repeat.IsVisible = false;
    }



    /// <summary>
    /// 上游 ShowAnimation：内容从 +320 滑到 0（0.6s）。用 <see cref="TranslateTransform"/> + <see cref="Transitions"/> 实现。
    /// </summary>
    private void StartShowAnimation()
    {
        long ts = Stopwatch.GetTimestamp();
        if (ts - _lastShowAnimationTs < (long)(0.6 * Stopwatch.Frequency))
        {
            return;
        }
        _lastShowAnimationTs = ts;
        EnsureTranslateTransform(WindowWidth, 0.6);
        _translateTransform!.X = 0;
    }


    private void StartHideAnimation()
    {
        EnsureTranslateTransform(WindowWidth, 0.6);
        _translateTransform!.X = WindowWidth;
    }


    private void EnsureTranslateTransform(double initialX, double seconds)
    {
        if (_translateTransform is null)
        {
            _translateTransform = new TranslateTransform(initialX, 0);
            _translateTransform.Transitions = new Transitions
            {
                new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromSeconds(seconds), Easing = new CubicEaseOut() },
            };
            RootGrid.RenderTransform = _translateTransform;
        }
    }


    /// <summary>
    /// 游戏窗口的客户区位置（X11：<c>xdotool getwindowgeometry --shell</c>；Wayland 拿不到时退回主屏右缘）。
    /// </summary>
    private (int X, int Y, int Width, int Height) GetClientBounds(nint hwnd)
    {
        if (hwnd != 0 && TryGetWindowGeometry(hwnd, out int x, out int y, out int w, out int h))
        {
            return (x, y, w, h);
        }
        var area = GetScreenWorkArea(0, 0);
        return (area.X, area.Y, area.Width, area.Height);
    }


    private static bool TryGetWindowGeometry(nint hwnd, out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        if (!ScreenCaptureHelper.IsToolAvailable("xdotool"))
        {
            return false;
        }
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "xdotool",
                ArgumentList = { "getwindowgeometry", "--shell", hwnd.ToString(CultureInfo.InvariantCulture) },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            if (process.ExitCode != 0)
            {
                return false;
            }
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int index = line.IndexOf('=');
                if (index < 0)
                {
                    continue;
                }
                string key = line[..index];
                string value = line[(index + 1)..];
                if (!int.TryParse(value, CultureInfo.InvariantCulture, out int number))
                {
                    continue;
                }
                switch (key)
                {
                    case "X": x = number; break;
                    case "Y": y = number; break;
                    case "WIDTH": width = number; break;
                    case "HEIGHT": height = number; break;
                }
            }
            return width > 0 && height > 0;
        }
        catch
        {
            return false;
        }
    }


    private PixelRect GetScreenWorkArea(int x, int y)
    {
        var screen = Screens.ScreenFromPoint(new PixelPoint(x, y)) ?? Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
        {
            return screen.WorkingArea;
        }
        return new PixelRect(0, 0, (int)Math.Max(Bounds.Width, WindowWidth), (int)Math.Max(Bounds.Height, WindowHeight));
    }



    private async void Button_OpenImage_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_lastFile))
            {
                return;
            }
            if (_imageViewWindow2 is null)
            {
                var item = new ScreenshotItem(_lastFile);
                _imageViewWindow2 = new ImageViewWindow2(item, new ObservableCollection<ScreenshotItem> { item });
            }
            _imageViewWindow2.Show();
            if (_finishedImageCount == _captureImageCount)
            {
                await HideWindowAsync(_openImageCancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Button_OpenImage_Click");
        }
    }


    private async void Button_OpenLog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? folder = Path.GetDirectoryName(AppConfig.LogFile);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                // 上游用 FolderLauncherOptions.ItemsToSelect 选中日志文件；Linux 没有等价 API，
                // 这里只打开日志目录（docs/09 §8.3）。
                Process.Start(new ProcessStartInfo { FileName = "xdg-open", ArgumentList = { folder }, UseShellExecute = false });
                if (_finishedImageCount == _captureImageCount)
                {
                    await HideWindowAsync(_openImageCancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Button_OpenLog_Click");
        }
    }

}
