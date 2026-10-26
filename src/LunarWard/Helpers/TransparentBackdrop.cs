using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LunarWard.Helpers;

/// <summary>
/// 全透明窗口背景（上游：src/Starward/Helpers/TransparentBackdrop.cs，SystemBackdrop + DWM blur + 子类化 WndProc）。
/// 平台替换：X11/Wayland 没有 DWM blur-behind，等价做法是把窗口设为透明并把背景置空，
/// 由内容自己画圆角/投影；不支持透明合成的合成器上退化为不透明黑底（不静默失败，写日志）。
/// 用法（移植后的窗口）：
/// <code>
/// var backdrop = new TransparentBackdrop();
/// backdrop.ApplyTo(this);
/// </code>
/// </summary>
public sealed class TransparentBackdrop
{


    /// <summary>把宿主窗口切成透明背景（对应上游 OnTargetConnected）。</summary>
    public void ApplyTo(Window window)
    {
        window.TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        window.Background = Brushes.Transparent;
        window.Opened += (_, _) =>
        {
            if (window.ActualTransparencyLevel != WindowTransparencyLevel.Transparent)
            {
                Serilog.Log.Warning("TransparentBackdrop: 当前合成器不支持透明窗口（ActualTransparencyLevel={Level}），退化为不透明背景。", window.ActualTransparencyLevel);
                window.Background = Brushes.Black;
            }
        };
    }


    /// <summary>恢复默认背景（对应上游 OnTargetDisconnected）。</summary>
    public void RemoveFrom(Window window)
    {
        window.Background = null;
    }


}
