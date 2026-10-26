using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using System;
using System.IO;

namespace LunarWard.Frameworks;

/// <summary>
/// 上游 <c>Starward.Frameworks.WindowEx</c> 的 Linux/Avalonia 等价基类。
/// </summary>
/// <remarks>
/// <para>
/// 上游在 WinUI 之上叠了一层 Win32 子类化（<c>SetWindowSubclass</c>、DWM 标题栏、非客户区拖拽矩形），
/// 目的是做自定义标题栏与窗口消息拦截。Avalonia 的窗口系统没有对等概念，因此本类只保留上游公开 API 中
/// 有语义的部分（Show/Hide/Minimize/CenterInScreen/SetIcon/主题判定/强调色），Win32 专有部分退化：
/// </para>
/// <list type="bullet">
/// <item><c>WindowSubclassProc</c>/<c>InputSiteSubclassProc</c>：Avalonia 无子类化回调，删除；
/// 需要拦截窗口消息的场景改用 Avalonia 的 <c>Window</c> 事件 / <c>PlatformImpl</c>。</item>
/// <item><c>SetDragRectangles</c>：Avalonia 用 <c>Window.BeginMoveDrag</c> 在 PointerPressed 里实现标题栏拖拽，
/// 本方法保留为空实现以免上游 XAML/代码改动。</item>
/// <item><c>AdaptTitleBarButtonColorToActuallTheme</c>：Avalonia 标题栏由窗口管理器绘制，无需适配。</item>
/// </list>
/// <para>平台差异已登记 docs/09-LINUX-DIVERGENCES.md。</para>
/// </remarks>
public abstract class WindowEx : Window
{

    /// <summary>上游用 <c>Microsoft.UI.WindowId</c>；Avalonia 里窗口标识就是 Window 实例本身，
    /// 需要 HWND 的场景（WebView 嵌入等）请用 <c>TryGetPlatformHandle()?.Handle</c>。</summary>
    public IntPtr WindowHandle => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;


    /// <summary>DPI 缩放：Avalonia 用 <see cref="TopLevel.RenderScaling"/>，语义与上游 <c>DpiForWindow/96</c> 一致。</summary>
    public double UIScale => RenderScaling;


    /// <summary>上游用于给对话框找 owner；这里保存主窗口实例。</summary>
    public static Window? MainWindow { get; protected set; }


    protected WindowEx()
    {
    }


    public virtual void Show()
    {
        base.Show();
        Activate();
    }


    public virtual void Hide()
    {
        base.Hide();
    }


    public virtual void Minimize()
    {
        WindowState = WindowState.Minimized;
    }


    public virtual void CenterInScreen(int? width = null, int? height = null)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }
        width = width <= 0 ? null : width;
        height = height <= 0 ? null : height;
        double scale = screen.Scaling;
        int w = (int)((width ?? ClientSize.Width) * scale);
        int h = (int)((height ?? ClientSize.Height) * scale);
        var area = screen.WorkingArea;
        int x = area.X + (area.Width - w) / 2;
        int y = area.Y + (area.Height - h) / 2;
        Position = new PixelPoint(x, y);
        if (width is > 0 && height is > 0)
        {
            ClientSize = new Size(width.Value, height.Value);
        }
    }


    public void SetIcon(string? iconPath = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(iconPath))
            {
                string? baseDirectory = AppContext.BaseDirectory;
                iconPath = Path.Join(baseDirectory, "Assets", "lunarward.ico");
            }
            if (File.Exists(iconPath))
            {
                Icon = new WindowIcon(iconPath);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Set window icon failed: {Path}", iconPath);
        }
    }


    /// <summary>上游用 AppWindow.TitleBar.SetDragRectangles 指定可拖拽区域；Avalonia 改为在 PointerPressed
    /// 里调用 <see cref="Window.BeginMoveDrag"/>，因此这里保留空实现以兼容上游调用点。</summary>
    public void SetDragRectangles(params PixelRect[] value)
    {
    }


    public void AdaptTitleBarButtonColorToActuallTheme()
    {
    }


    public virtual void ChangeAccentColor(Color? backColor = null, Color? foreColor = null)
    {
    }


    /// <summary>上游通过注册表读取「应用使用深色模式」；Linux 走 XDG 桌面门户的配色偏好
    /// （<c>org.freedesktop.appearance color-scheme</c>，Avalonia 已在 <see cref="Application.PlatformSettings"/> 里暴露）。</summary>
    protected static bool ShouldAppsUseDarkMode()
    {
        return Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
    }


    protected static bool ShouldSystemUseDarkMode()
    {
        return ShouldAppsUseDarkMode();
    }

}
