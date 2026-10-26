using Avalonia.Controls;

namespace LunarWard.Frameworks;

/// <summary>
/// <c>Microsoft.UI.Xaml.Controls.Frame</c> 的等价实现（只保留上游用到的能力）。
/// 上游 <c>Frame.Navigate(Type, object?, NavigationTransitionInfo?)</c> 会创建页面、把参数写入
/// <c>DataContext</c>、再触发 <c>OnNavigatedTo</c>。本仓库页面普遍在构造函数里
/// <c>DataContext = this</c>（Avalonia 绑定的通行写法），因此这里**不写 DataContext**，
/// 只负责实例化 + <see cref="PageBase.OnNavigatedTo"/>，参数语义与上游一致（GameId / GameBiz）。
/// 平台差异见 docs/09「五、Features/ViewHost」。
/// </summary>
public class Frame : ContentControl
{

    /// <summary>当前页面类型，对应上游 <c>Frame.SourcePageType</c>。</summary>
    public Type? SourcePageType { get; private set; }


    /// <summary>当前页面参数，对应上游 <c>Frame</c> 的当前导航参数。</summary>
    public object? CurrentParameter { get; private set; }


    public void Navigate(Type pageType, object? parameter = null, NavigationTransitionInfo? infoOverride = null)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (Activator.CreateInstance(pageType) is not Control page)
        {
            throw new InvalidOperationException($"Page type {pageType.FullName} is not a Control.");
        }
        SourcePageType = pageType;
        CurrentParameter = parameter;
        Content = page;
        if (page is PageBase pageBase)
        {
            pageBase.OnNavigatedTo(parameter);
        }
    }

}



/// <summary>导航过渡基类（Avalonia 无等价物，保留类型以便上游代码原样编译）。</summary>
public abstract class NavigationTransitionInfo
{
}


/// <summary>抑制过渡（等价于不做动画）。</summary>
public sealed class SuppressNavigationTransitionInfo : NavigationTransitionInfo
{
}
