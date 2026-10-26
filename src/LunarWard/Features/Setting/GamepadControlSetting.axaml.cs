using LunarWard.Frameworks;

namespace LunarWard.Features.Setting;

/// <summary>
/// 手柄增强页（对应 Starward <c>GamepadControlSetting.xaml</c>）。
/// </summary>
/// <remarks>
/// Starward 依赖 Windows 的 GameInput / XInput 模拟与全局按键注入。Linux 没有等价且
/// 稳定的方案（Steam Input 是另一个层次的东西），因此这个菜单项<b>保留</b>以对齐排版，
/// 但页面只写清楚现状，<b>不伪造任何开关</b>。
/// </remarks>
public sealed partial class GamepadControlSetting : PageBase
{

    public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    public GamepadControlSetting()
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();
        DataContext = this;
    }
}
