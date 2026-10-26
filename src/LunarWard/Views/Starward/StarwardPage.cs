using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LunarWard.Services;
using LunarWard.ViewModels;
using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward;

/// <summary>
/// Starward 页面的公共基类：持有 <see cref="PageContext"/>，并自动创建并销毁页面 VM。
/// </summary>
/// <remarks>
/// 页面 VM 都实现 <see cref="IDisposable"/>，卸载时由基类统一释放，页面代码不需要关心。
/// </remarks>
public abstract class StarwardPage : UserControl
{
    protected StarwardPage(PageContext context)
    {
        Context = context;
    }


    public PageContext Context { get; }


    /// <summary>子类覆盖以释放自己的 VM（默认无动作）。</summary>
    protected virtual void DisposeViewModel(PageContext context)
    {
    }


    /// <summary>页面首次挂载后调用（拉数据、起定时器）。</summary>
    public virtual void OnNavigatedTo()
    {
    }


    /// <summary>页面卸载时调用（释放 VM、停定时器）。</summary>
    public virtual void OnNavigatedFrom()
    {
        DisposeViewModel(Context);
    }
}
