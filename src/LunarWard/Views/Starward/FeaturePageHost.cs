using Avalonia.Controls;

// 临时桥接：旧外壳（--screenshot/--page 自检路径）需要 StarwardPage 派生类，而 Features 下的页面
// 已按上游改为 Frameworks.PageBase + 无参构造。正常启动已走 Features/ViewHost/MainWindow（App.StartPortShell），
// 这里只服务于尚未迁移的自检路由；旧壳整体删除时本文件一并删除（docs/10 §7）。
namespace LunarWard.Views.Starward;

/// <summary>把 <see cref="Features"/> 下的页面塞进旧壳的宿主控件。</summary>
public class FeaturePageHost(PageContext context, Control content) : StarwardPage(context)
{
    private readonly Control _content = content;

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Content is null)
        {
            Content = _content;
        }
    }
}
