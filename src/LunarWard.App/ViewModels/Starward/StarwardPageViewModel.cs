using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.App.Services;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// Starward 页面 VM 的基类：统一的 <see cref="Context"/>、<see cref="Strings"/>、
/// <see cref="Services"/> 入口。
/// </summary>
/// <remarks>
/// 页面 VM 一律 <c>sealed partial class XxxPageViewModel : StarwardPageViewModel</c>，
/// 构造函数接收 <see cref="PageContext"/> 并以 <c>base(context)</c> 透传。
/// </remarks>
public abstract partial class StarwardPageViewModel : ObservableObject, IDisposable
{
    protected StarwardPageViewModel(PageContext context)
    {
        Context = context;
    }


    /// <summary>页面上下文（服务、外壳、当前档案、日志）。</summary>
    public PageContext Context { get; }

    /// <summary>外壳 ViewModel（页面内跳转、弹窗、Toast 都从这里走）。</summary>
    public ShellViewModel Shell => Context.Shell;

    /// <summary>应用服务集合。</summary>
    public AppServices Services => Context.Services;

    /// <summary>当前游戏档案（切换游戏后页面会被重建）。</summary>
    public ViewModels.ProfileItemViewModel Profile => Context.Profile;

    /// <summary>日志。</summary>
    public LogService Log => Context.Log;

    /// <summary>XAML 文案索引器：<c>{Binding Strings[Settings.General]}</c>。</summary>
    public Localization.AppStrings Strings => Localization.AppStrings.Instance;


    public virtual void Dispose()
    {
    }
}
