using Avalonia.Controls;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

/// <summary>
/// 设置子页的公共基类：每个子页是一个 <see cref="UserControl"/>，
/// DataContext 是它自己的 <see cref="SettingSectionViewModel"/>。
/// </summary>
/// <remarks>
/// 设置页持有子页实例的字典（切换回来不重建），所以这里提供 <see cref="Dispose"/>，
/// 由 <c>SettingPage.DisposeViewModel</c> 统一调用。
/// </remarks>
public abstract class SettingSectionView : UserControl
{
    /// <summary>子页卸载（设置页被换掉）时调用，用于取消订阅。</summary>
    public virtual void Dispose()
    {
    }


    /// <summary>按 VM 类型创建对应的子页视图。</summary>
    public static SettingSectionView Create(SettingSectionViewModel section) => section switch
    {
        SettingAboutViewModel vm => new SettingAboutView(vm),
        SettingGeneralViewModel vm => new SettingGeneralView(vm),
        SettingDownloadViewModel vm => new SettingDownloadView(vm),
        SettingFileManageViewModel vm => new SettingFileManageView(vm),
        SettingScreenshotViewModel vm => new SettingScreenshotView(vm),
        SettingHotkeyViewModel vm => new SettingHotkeyView(vm),
        SettingGamepadViewModel vm => new SettingGamepadView(vm),
        SettingAdvancedViewModel vm => new SettingAdvancedView(vm),
        SettingToolboxViewModel vm => new SettingToolboxView(vm),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section.Kind, "未知的设置子页"),
    };
}
