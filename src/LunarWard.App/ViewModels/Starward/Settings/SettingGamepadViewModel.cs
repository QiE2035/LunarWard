using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 手柄增强页（对应 Starward <c>GamepadControlSetting.xaml</c>）。
/// </summary>
/// <remarks>
/// Starward 依赖 Windows 的 GameInput / XInput 模拟与全局按键注入。Linux 没有等价且
/// 稳定的方案（Steam Input 是另一个层次的东西），因此这个菜单项<b>保留</b>以对齐排版，
/// 但页面只写清楚现状，<b>不伪造任何开关</b>。
/// </remarks>
public sealed class SettingGamepadViewModel : SettingSectionViewModel
{
    public SettingGamepadViewModel(PageContext context, bool showDivider = false)
        : base(context, SettingSectionKind.Gamepad, "Settings.ControllerEnhancement", "\uE7C4", showDivider)
    {
    }
}
