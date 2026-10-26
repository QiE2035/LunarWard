using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LunarWard.Features.Screenshot;


/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ScrollViewerExtensions</c>：在 ScrollViewer 的缩放率大于 1 时忽略鼠标滚轮操作。
/// </summary>
/// <remarks>
/// 差异（docs/09）：上游通过 WinRT 私有接口 <c>IScrollViewerPrivate</c>（COM 虚表偏移）设置
/// <c>ArePointerWheelEventsIgnored</c>；Avalonia 没有这个私有接口，这里用附加属性 + 隧道阶段
/// <see cref="InputElement.PointerWheelChangedEvent"/> 处理器把滚轮事件标记为已处理，
/// 语义等价（滚动条不再因为滚轮而滚动，缩放逻辑自己处理滚轮）。
/// </remarks>
public static class ScrollViewerExtensions
{

    private static readonly AttachedProperty<bool> ArePointerWheelEventsIgnoredProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("ArePointerWheelEventsIgnored", typeof(ScrollViewerExtensions));


    static ScrollViewerExtensions()
    {
        ArePointerWheelEventsIgnoredProperty.Changed.AddClassHandler<ScrollViewer>((scrollViewer, e) =>
        {
            if (e.NewValue is true)
            {
                scrollViewer.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        });
    }


    private static void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer && GetArePointerWheelEventsIgnored(scrollViewer))
        {
            e.Handled = true;
        }
    }


    public static bool GetArePointerWheelEventsIgnored(this ScrollViewer scrollViewer)
    {
        return scrollViewer.GetValue(ArePointerWheelEventsIgnoredProperty);
    }

    public static void SetArePointerWheelEventsIgnored(this ScrollViewer scrollViewer, bool value)
    {
        scrollViewer.SetValue(ArePointerWheelEventsIgnoredProperty, value);
    }

}
