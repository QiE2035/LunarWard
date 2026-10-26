using Avalonia.Controls;
using Avalonia.Media;

namespace LunarWard.Views.Starward;

/// <summary>
/// 窗口级文字渲染开关。
/// </summary>
/// <remarks>
/// Linux 上 Skia 默认可能走 LCD 子像素抗锯齿，深色背景 + 浅色字会出红/蓝彩边
/// （截图放大后可见）。Starward 在 Windows 上由系统 ClearType 决定，这里统一用
/// 灰度抗锯齿，跨平台观感更稳。
///
/// 注意：<see cref="TextOptions.TextRenderingMode"/> 在 Avalonia 12 里没有公开的
/// <c>TextRenderingModeProperty</c> 字段，所以不能写成 <c>&lt;Style Selector="Window"&gt;</c>
/// 的 Setter；XAML 里用 <c>TextOptions.TextRenderingMode="Antialias"</c> 属性即可，
/// 代码里动态创建的窗口调用本方法。
/// </remarks>
public static class UiTypography
{
    public static void ApplyGrayscaleAntialias(Window window)
    {
        TextOptions.SetTextRenderingMode(window, TextRenderingMode.Antialias);
    }
}
