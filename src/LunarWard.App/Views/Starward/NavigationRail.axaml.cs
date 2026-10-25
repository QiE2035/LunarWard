using Avalonia.Controls;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// Starward 左栏：固定 56px 图标模式 + 悬停 Tooltip。
/// </summary>
/// <remarks>
/// Starward 的 <c>NavigationView</c> 设了 <c>PaneDisplayMode="LeftCompact"</c> 且
/// <c>IsPaneToggleButtonVisible="False"</c>，所以面板**不会**展开——只有图标和 Tooltip。
/// 早期实现按"悬停展开"做过一版，与 Starward 的实际行为不符，已回退。
/// </remarks>
public partial class NavigationRail : UserControl
{
    public NavigationRail()
    {
        InitializeComponent();
    }}
