using Avalonia.Controls;

namespace LunarWard.Frameworks;

/// <summary>
/// <c>Microsoft.UI.Xaml.Controls.Pivot</c> 的等价实现（只保留上游用到的能力）。
/// </summary>
/// <remarks>
/// 上游 <c>Pivot</c> 与 Avalonia <see cref="TabControl"/> 语义一致：Headers 横向排列、
/// 选中项决定内容区、支持 <c>SelectionChanged</c>。本类只做类型映射，让
/// <c>Features/GameLauncher/GameLauncherSettingDialog.axaml</c> 直接照抄上游标签名，
/// 不把 XAML 改写成 <c>TabControl</c>（那样会制造结构偏移，见 docs/10 §1）。
/// 平台差异见 docs/09。
/// </remarks>
public class Pivot : TabControl
{
}


/// <summary><c>Microsoft.UI.Xaml.Controls.PivotItem</c> 的等价实现。</summary>
public class PivotItem : TabItem
{
}
