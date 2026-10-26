using System.Globalization;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 图片查看器的纯格式化逻辑（Starward <c>Features/Screenshot/ImageViewWindow2.xaml.cs</c>）：
/// 缩放百分比、<c>1/12</c> 索引、像素大小、文件大小。
/// </summary>
/// <remarks>
/// 抽成静态类是为了让数值格式（含边界）能被单测覆盖；窗口只负责把状态塞进来。
/// </remarks>
public static class ImageViewWindow2State
{

    /// <summary>Starward <c>ScrollViewer_Image.ZoomFactor.ToString('P0')</c>：15% / 100%。</summary>
    public static string ZoomPercent(double zoomFactor)
        => Math.Round(zoomFactor * 100, MidpointRounding.AwayFromZero)
            .ToString("0", CultureInfo.InvariantCulture) + "%";


    /// <summary>Starward <c>TextBlock_ImageIndex</c>：<c>{SelectedIndex + 1}/{Count}</c>，无图时为空。</summary>
    public static string IndexText(int zeroBasedIndex, int count)
        => count <= 0 || zeroBasedIndex < 0 ? "" : $"{zeroBasedIndex + 1}/{count}";


    /// <summary>Starward <c>CurrentPixelSizeText</c>：<c>1920 × 1080</c>；未知尺寸返回空。</summary>
    public static string PixelSizeText(int width, int height)
        => width <= 0 || height <= 0 ? "" : $"{width} × {height}";


    /// <summary>Starward <c>CurrentFileSizeText</c>：B / KiB / MiB / GiB，两位小数以内。</summary>
    public static string FileSizeText(long bytes)
    {
        if (bytes < 0)
        {
            return "";
        }
        const double KiB = 1 << 10;
        const double MiB = 1 << 20;
        const double GiB = 1 << 30;
        if (bytes >= GiB)
        {
            return $"{bytes / GiB:0.##} GiB";
        }
        if (bytes >= MiB)
        {
            return $"{bytes / MiB:0.##} MiB";
        }
        return bytes >= KiB ? $"{bytes / KiB:0.#} KiB" : $"{bytes} B";
    }


    /// <summary>缩放下限/上限（Starward <c>Slider_ZoomFactor Minimum=0.1 Maximum=5</c>）。</summary>
    public const double MinZoom = 0.1;

    public const double MaxZoom = 5.0;


    /// <summary>把任意缩放系数夹到 <see cref="MinZoom"/>..<see cref="MaxZoom"/>。</summary>
    public static double ClampZoom(double zoomFactor)
        => double.IsNaN(zoomFactor) ? 1.0 : Math.Clamp(zoomFactor, MinZoom, MaxZoom);


    /// <summary>Starward <c>Button_ZoomIn_Click</c> 的档位：每次 ×1.25。</summary>
    public static double ZoomIn(double zoomFactor) => ClampZoom(ClampZoom(zoomFactor) * 1.25);


    /// <summary>Starward <c>Button_ZoomOut_Click</c> 的档位：每次 ÷1.25。</summary>
    public static double ZoomOut(double zoomFactor) => ClampZoom(ClampZoom(zoomFactor) / 1.25);

}
