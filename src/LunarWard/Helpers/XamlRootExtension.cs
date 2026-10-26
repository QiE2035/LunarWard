using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace LunarWard.Helpers;

/// <summary>
/// 上游：src/Starward/Helpers/XamlRootExtension.cs（AppWindow / TitleBar.SetDragRectangles / RasterizationScale）。
/// 平台替换：Avalonia 没有「窗口拖拽矩形」概念（X11 下拖标题栏由窗口管理器负责，自绘标题栏用
/// BeginMoveDrag），所以 SetWindowDragRectangles 保留形状但不生效；缩放与窗口句柄走 TopLevel/Win32。
/// </summary>
internal static class XamlRootExtension
{


    /// <summary>对应上游 <c>XamlRoot.SetWindowDragRectangles(params RectInt32[])</c>。</summary>
    public static void SetWindowDragRectangles(this TopLevel? topLevel, params Rect[] rects)
    {
        // 平台限制：Avalonia/X11 没有 drag rectangle 机制。
    }



    /// <summary>对应上游 <c>XamlRoot.GetUIScaleFactor()</c>（= RasterizationScale）。</summary>
    public static double GetUIScaleFactor(this TopLevel? topLevel)
    {
        if (topLevel is null)
        {
            return 1;
        }
        return topLevel.RenderScaling;
    }



    /// <summary>对应上游 <c>XamlRoot.GetAppWindow()</c>：Avalonia 里就是宿主 Window。</summary>
    public static Window? GetAppWindow(this TopLevel? topLevel) => topLevel as Window;


    /// <summary>对应上游 <c>XamlRoot.GetWindowHandle()</c>：X11 上是 XID（Wayland 上为 0）。</summary>
    public static nint GetWindowHandle(this TopLevel? topLevel)
    {
        if (topLevel?.TryGetPlatformHandle() is { } handle)
        {
            return handle.Handle;
        }
        return 0;
    }



    /// <summary>从任意 Visual（上游是 FrameworkElement）找到宿主 TopLevel。</summary>
    public static TopLevel? FindTopLevel(this Visual visual) => TopLevel.GetTopLevel(visual);


}
