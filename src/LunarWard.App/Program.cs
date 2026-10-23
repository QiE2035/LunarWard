using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;

namespace LunarWard.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 自检开关：--screenshot <png> 打开主窗口、切到指定页面、渲染一帧、写 PNG 后退出。
        // 用于 CI/无人值守验证，不改变正常启动行为。
        //
        //   --screenshot <png>                 启动器页（默认）
        //   --page launcher|game-setting|screenshot|gacha|toolbox|settings
        //   --profile <id>                     指定档案（如 genshin-cn）
        //   --wait <ms>                        渲染前等待（默认 1600，给网络素材留时间）
        //   --window <name>                    旧窗口自检（settings / account / dailynote / gacha）
        var index = Array.IndexOf(args, "--screenshot");
        if (index >= 0 && index + 1 < args.Length)
        {
            CaptureMode.Path = args[index + 1];
            CaptureMode.TargetWindow = ReadArgument(args, "--window") ?? "main";
            CaptureMode.Page = ReadArgument(args, "--page");
            CaptureMode.ProfileId = ReadArgument(args, "--profile");
            CaptureMode.OpenSelector = args.Contains("--open-selector");
            CaptureMode.GameIcons = args.Contains("--game-icons");
            CaptureMode.Tab = ReadArgument(args, "--tab");
            CaptureMode.Language = ReadArgument(args, "--lang");
            CaptureMode.ExpandRail = args.Contains("--expand-rail");
            if (int.TryParse(ReadArgument(args, "--wait"), out var wait) && wait >= 0)
            {
                CaptureMode.WaitMilliseconds = wait;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }


    private static string? ReadArgument(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }


    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Starward 在 Windows 上靠系统字体回退把中文交给「微软雅黑」渲染；
            // Avalonia/Skia 不做隐式回退，这里显式给出回退链，效果与 Windows 一致。
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "Segoe UI",
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("Microsoft YaHei UI") },
                    new FontFallback { FontFamily = new FontFamily("Noto Sans CJK SC") },
                    new FontFallback { FontFamily = new FontFamily("Segoe MDL2 Assets") },
                    new FontFallback { FontFamily = new FontFamily("Segoe Fluent Icons") },
                ],
            })
            .LogToTrace();
}


/// <summary>自检模式状态（<c>--screenshot</c>）。</summary>
internal static class CaptureMode
{
    public static string? Path { get; set; }

    /// <summary>要截图的旧窗口：<c>main</c>（Starward 外壳）/ settings / account / dailynote / gacha。</summary>
    public static string TargetWindow { get; set; } = "main";

    /// <summary>要切到的 Starward 页面（<c>launcher</c> 等），null = 启动器页。</summary>
    public static string? Page { get; set; }

    /// <summary>指定档案 id。</summary>
    public static string? ProfileId { get; set; }

    /// <summary>截图前打开游戏选择层。</summary>
    public static bool OpenSelector { get; set; }

    /// <summary>截图前展开左上角横排游戏图标条（模拟悬停当前游戏图标）。</summary>
    public static bool GameIcons { get; set; }

    /// <summary>工具箱页的 Tab：<c>note</c>（默认）/ <c>accounts</c>。</summary>
    public static string? Tab { get; set; }

    /// <summary>自检时的界面语言（<c>zh-CN</c> / <c>en-US</c>）。</summary>
    public static string? Language { get; set; }

    /// <summary>截图前把左栏展开（模拟鼠标悬停）。</summary>
    public static bool ExpandRail { get; set; }

    /// <summary>渲染前等待时间（毫秒）。</summary>
    public static int WaitMilliseconds { get; set; } = 1600;

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
        }, TimeSpan.FromMilliseconds(WaitMilliseconds));
    }
}
