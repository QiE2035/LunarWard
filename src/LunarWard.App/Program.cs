using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace LunarWard.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 自检开关：--screenshot <png> 打开主窗口、渲染一帧、写 PNG 后退出。
        // 用于 CI/无人值守验证，不改变正常启动行为。
        var index = Array.IndexOf(args, "--screenshot");
        if (index >= 0 && index + 1 < args.Length)
        {
            // --screenshot <png>            主窗口
            // --screenshot <png> --window settings   设置窗口
            CaptureMode.Path = args[index + 1];
            var windowIndex = Array.IndexOf(args, "--window");
            if (windowIndex >= 0 && windowIndex + 1 < args.Length)
            {
                CaptureMode.TargetWindow = args[windowIndex + 1];
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }


    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}


/// <summary>自检模式状态（<c>--screenshot</c>）。</summary>
internal static class CaptureMode
{
    public static string? Path { get; set; }

    /// <summary>要截图的窗口：<c>main</c>（默认）/ <c>settings</c> / <c>gacha</c>。</summary>
    public static string TargetWindow { get; set; } = "main";

    public static bool Enabled => Path is not null;


    /// <summary>渲染窗口到 PNG 并退出。</summary>
    public static void Capture(Window window)
    {
        if (Path is not { } path)
        {
            return;
        }

        DispatcherTimer.RunOnce(() =>
        {
            try
            {
                var size = new PixelSize(
                    Math.Max(1, (int)window.Bounds.Width),
                    Math.Max(1, (int)window.Bounds.Height));
                using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
                bitmap.Render(window);
                using (var stream = File.Create(path))
                {
                    bitmap.Save(stream, new PngBitmapEncoderOptions());
                }
                Console.WriteLine($"SCREENSHOT_OK {path} {size.Width}x{size.Height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SCREENSHOT_FAIL {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(0);
                }
            }
        }, TimeSpan.FromMilliseconds(900));
    }
}
