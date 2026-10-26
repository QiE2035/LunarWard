using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using LunarWard.Language;
using LunarWard.Localization;
using LunarWard.Services;
using LunarWard.Views.Starward;

namespace LunarWard.Features.Setting;

/// <summary>
/// 设置功能各子页的公共基类，对应上游 Starward 里 <c>Frameworks/PageBase</c> + 页面自身承担的角色。
/// </summary>
/// <remarks>
/// 上游 <c>Starward/src/Starward/Features/Setting/*.xaml.cs</c> 都是 code-behind 直接放属性、命令和业务逻辑，
/// <c>{x:Bind}</c> 指向页面自己（等价于 <c>DataContext = this</c>）。本仓库照此落点：
/// 每个设置页的 <c>DataContext</c> 都在自己的构造函数末尾设为 <c>this</c>，成员全部写在页面的 <c>.axaml.cs</c> 里。
/// <para>
/// 为什么需要这一层：Avalonia 的 <see cref="Avalonia.AvaloniaObject"/> 虽然实现了
/// <see cref="INotifyPropertyChanged"/>，但并不提供 <c>OnPropertyChanged(string)</c> /
/// <c>OnPropertyChanged(PropertyChangedEventArgs)</c>（它只有 <c>OnPropertyChanged(AvaloniaPropertyChangedEventArgs)</c>），
/// MVVM Toolkit 的 <c>[ObservableProperty]</c> 生成的代码无法直接落在 <see cref="Avalonia.Controls.UserControl"/> 上。
/// 因此这里补一个最小的通知层：只发 <see cref="INotifyPropertyChanged"/>，不碰 Avalonia 的属性系统。
/// </para>
/// </remarks>
public abstract class SettingPageBase : StarwardPage, INotifyPropertyChanged
{
    protected SettingPageBase(PageContext context)
        : base(context)
    {
        SyncLanguage();
    }


    /// <summary>
    /// 把配置里的界面语言同步到上游式 <see cref="Lang"/> 资源文化。
    /// <para>
    /// 上游用 <c>ResourceManager</c> 的 <c>Culture</c> 选语言；本仓库设置页完成 i18n 迁移后
    /// 也走同一套，所以进入任何设置子页前都要先对齐，否则会按系统文化渲染。
    /// </para>
    /// </summary>
    protected void SyncLanguage()
    {
        var code = LunarWard.Localization.AppStrings.Normalize(Context.Services.Config.Launcher.Language);
        Lang.Culture = System.Globalization.CultureInfo.GetCultureInfo(code);
    }


    /// <summary>
    /// 重新声明通知事件，让绑定系统拿到本类的实现。
    /// <see cref="Avalonia.AvaloniaObject"/> 上的同名事件只由控件自身触发（Avalonia 属性变更），
    /// 这里显式重新映射 <see cref="INotifyPropertyChanged"/>，用于本页 CLR 属性的通知。
    /// </summary>
    public new event PropertyChangedEventHandler? PropertyChanged;


    /// <summary>
    /// 显式重映射接口事件：<see cref="Avalonia.AvaloniaObject"/> 已经隐式实现过
    /// <see cref="INotifyPropertyChanged"/>，不重新声明的话接口分发仍会落到基类的事件上，
    /// 页面自己的 <c>SetProperty</c> 就通知不到绑定。
    /// </summary>
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropertyChanged += value;
        remove => PropertyChanged -= value;
    }


    /// <summary>应用服务集合。</summary>
    public AppServices Services => Context.Services;

    /// <summary>外壳 VM（页面跳转 / 对话框）。</summary>
    public LunarWard.ViewModels.Starward.ShellViewModel Shell => Context.Shell;

    /// <summary>当前游戏档案。</summary>
    public LunarWard.ViewModels.ProfileItemViewModel Profile => Context.Profile;

    /// <summary>日志。</summary>
    public LogService Log => Context.Log;

    /// <summary>XAML 文案索引器：<c>{Binding Strings[Settings.About.Title]}</c>。</summary>
    public AppStrings Strings => AppStrings.Instance;


    /// <summary>错误/状态行（上游各设置页末尾的 InfoBar 文本）。</summary>
    public string? Status
    {
        get => _status;
        set
        {
            if (!SetProperty(ref _status, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);


    private string? _status;


    /// <summary>
    /// 设置字段并发通知（<c>[ObservableProperty]</c> 的等价手写版：Avalonia 页面不是
    /// <c>ObservableObject</c>，Toolkit 的源生成器不支持，所以统一走这里）。
    /// </summary>
    /// <returns><see langword="true"/> 表示值发生变化。</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }


    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    protected void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }


    /// <summary>MVVM Toolkit 生成的属性 setter 会调用；设置页不做 PropertyChanging 通知，留空。</summary>
    protected void OnPropertyChanging(PropertyChangingEventArgs e)
    {
    }
}
