using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using LunarWard.App.Localization;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// Starward <c>DX12IntroDialog</c> 的等价物（启动器 DX12 开关旁的「详情」）。
/// </summary>
/// <remarks>
/// 文案直接按当前语言给字面量，避免为一次性弹窗再往两份字典里插键（前几轮手插字典出过错）。
/// </remarks>
public partial class DX12IntroDialog : Window
{
    public DX12IntroDialog()
    {
        InitializeComponent();
        bool zh = AppStrings.Instance.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        Heading = zh ? "DX12 效果详情" : "DX12 effect details";
        RegularGraphics = zh ? "常规画质" : "Regular graphics";
        EnhancedGraphics = zh ? "DX12 增强画质" : "DX12 enhanced graphics";
        Description = zh
            ? "开启后游戏将以 DirectX 12 渲染，支持更完整的光追与更稳定的帧时间；关闭后回到 DirectX 11。切换会重新进入游戏才生效。"
            : "When enabled the game renders with DirectX 12, giving fuller ray tracing and steadier frame times; disabling returns to DirectX 11. The change takes effect after relaunching the game.";
        DataContext = this;
    }

    public string Heading { get; }

    public string RegularGraphics { get; }

    public string EnhancedGraphics { get; }

    public string Description { get; }


    /// <summary>自检：把窗口渲染成 PNG（<c>--screenshot ... --dx12-intro</c>）。</summary>
    public void CaptureSelf(string path, int delayMs = 500)
    {
        Avalonia.Threading.DispatcherTimer.RunOnce(() =>
        {
            try
            {
                var size = new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
                using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
                bitmap.Render(this);
                using (var stream = File.Create(path))
                {
                    bitmap.Save(stream, new PngBitmapEncoderOptions());
                }
                Console.WriteLine($"SCREENSHOT_OK {path} {size.Width}x{size.Height}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"SCREENSHOT_FAIL {path} {ex.Message}");
            }
            finally
            {
                Close();
                if (Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(0);
                }
            }
        }, TimeSpan.FromMilliseconds(delayMs));
    }


    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
