using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Linq;


namespace LunarWard.Features.Gacha;

/// <summary>
/// 上游 <c>Starward.Features.Gacha.GachaStatsCard</c> 的移植。
/// WinUI 依赖属性 → Avalonia <see cref="StyledProperty{TValue}"/>；
/// <c>Pivot + cwc:Segmented</c> → 隐藏表头的 <see cref="TabControl"/> + 两个 <see cref="ToggleButton"/>（见 docs/09）。
/// </summary>
public partial class GachaStatsCard : UserControl
{

    public GachaStatsCard()
    {
        InitializeComponent();
    }



    public GachaTypeStats WarpTypeStats
    {
        get { return GetValue(WarpTypeStatsProperty); }
        set { SetValue(WarpTypeStatsProperty, value); }
    }

    public static readonly StyledProperty<GachaTypeStats> WarpTypeStatsProperty =
        AvaloniaProperty.Register<GachaStatsCard, GachaTypeStats>(nameof(WarpTypeStats));



    private void Grid_Rarity5Item_PointerEntered(object? sender, PointerEventArgs e)
    {
        try
        {
            if (sender is Control ele && ele.Tag is GachaLogItemEx item)
            {
                if (WarpTypeStats?.List_5?.Any() ?? false)
                {
                    foreach (var l5 in WarpTypeStats.List_5)
                    {
                        l5.IsPointerIn = (l5.Name == item.Name);
                    }
                }
            }
        }
        catch { }
    }


    private void Grid_Rarity5Item_PointerExited(object? sender, PointerEventArgs e)
    {
        try
        {
            if (sender is Control ele && ele.Tag is GachaLogItemEx item)
            {
                if (WarpTypeStats?.List_5?.Any() ?? false)
                {
                    foreach (var l5 in WarpTypeStats.List_5)
                    {
                        l5.IsPointerIn = false;
                    }
                }
            }
        }
        catch { }
    }


    private void Grid_Rarity4Item_PointerEntered(object? sender, PointerEventArgs e)
    {
        try
        {
            if (sender is Control ele && ele.Tag is GachaLogItemEx item)
            {
                if (WarpTypeStats?.List_4?.Any() ?? false)
                {
                    foreach (var l5 in WarpTypeStats.List_4)
                    {
                        l5.IsPointerIn = (l5.Name == item.Name);
                    }
                }
            }
        }
        catch { }
    }


    private void Grid_Rarity4Item_PointerExited(object? sender, PointerEventArgs e)
    {
        try
        {
            if (sender is Control ele && ele.Tag is GachaLogItemEx item)
            {
                if (WarpTypeStats?.List_4?.Any() ?? false)
                {
                    foreach (var l5 in WarpTypeStats.List_4)
                    {
                        l5.IsPointerIn = false;
                    }
                }
            }
        }
        catch { }
    }



    private void SegmentedItem_Rarity5_Click(object? sender, RoutedEventArgs e)
    {
        Segmented_GachaItemList.SelectedIndex = 0;
    }


    private void SegmentedItem_Rarity4_Click(object? sender, RoutedEventArgs e)
    {
        Segmented_GachaItemList.SelectedIndex = 1;
    }


    private void Segmented_GachaItemList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ToggleButton_Rarity5 is null || ToggleButton_Rarity4 is null)
        {
            return;
        }
        ToggleButton_Rarity5.IsChecked = Segmented_GachaItemList.SelectedIndex == 0;
        ToggleButton_Rarity4.IsChecked = Segmented_GachaItemList.SelectedIndex == 1;
    }


    public void ResetGachaTypeTextFontSize()
    {
        TextBlock_GachaTypeText.FontSize = 16;
    }

}
