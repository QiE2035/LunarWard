using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using LunarWard.ViewModels.Starward;

namespace LunarWard.Controls;

/// <summary>
/// 绝区零「调频记录」统计卡（Starward <c>Features/Gacha/ZZZGachaStatsCard.xaml</c> 的 Avalonia 移植）。
/// </summary>
/// <remarks>
/// DataContext 是 <see cref="GachaPoolCardViewModel"/>；列表行的悬停高亮照
/// <c>GachaStatsCard.xaml.cs:29-102</c>：同名的行一起变色。
/// </remarks>
public partial class ZZZGachaStatsCard : UserControl
{
    public ZZZGachaStatsCard()
    {
        InitializeComponent();
    }


    private void OnRarity5RowPointerEntered(object? sender, PointerEventArgs e)
        => SetPointerIn(sender, fiveStar: true, value: true);

    private void OnRarity5RowPointerExited(object? sender, PointerEventArgs e)
        => SetPointerIn(sender, fiveStar: true, value: false);

    private void OnRarity4RowPointerEntered(object? sender, PointerEventArgs e)
        => SetPointerIn(sender, fiveStar: false, value: true);

    private void OnRarity4RowPointerExited(object? sender, PointerEventArgs e)
        => SetPointerIn(sender, fiveStar: false, value: false);


    private void SetPointerIn(object? sender, bool fiveStar, bool value)
    {
        if (sender is Control { DataContext: GachaStatsItemViewModel item }
            && DataContext is GachaPoolCardViewModel card)
        {
            if (fiveStar)
            {
                card.SetPointerIn5(item.Name, value);
            }
            else
            {
                card.SetPointerIn4(item.Name, value);
            }
        }
    }
}
