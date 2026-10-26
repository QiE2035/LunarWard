using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using LunarWard.ViewModels.Starward;

namespace LunarWard.Controls;

/// <summary>
/// 卡池统计卡（Starward <c>Features/Gacha/GachaStatsCard.xaml</c> 的 Avalonia 移植）。
/// </summary>
/// <remarks>
/// DataContext 是 <see cref="GachaPoolCardViewModel"/>；列表行的悬停高亮照
/// <c>GachaStatsCard.xaml.cs:29-102</c>：同名的行一起变色。
/// </remarks>
public partial class GachaStatsCard : UserControl
{
    public GachaStatsCard()
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


/// <summary>
/// 5★ 列表里「保底进度」底色（Starward <c>GachaPityProgressBackgroundBrushConverter.cs</c> 原样移植）：
/// 进度 = 已垫抽数 / 保底抽数，未到阈值绿色、接近保底红色，横向渐变、整体 Opacity 0.4。
/// </summary>
/// <remarks>
/// 阈值照源码：普通池 74/90；武器池（原神 302 / 绝区零 3、103、5）63 或 65 / 80。
/// 颜色照源码：红色 #C83C23、绿色 #00E079。
/// </remarks>
public sealed class GachaPityProgressBackgroundBrushConverter : IValueConverter
{
    private static readonly Color Red = Color.FromRgb(0xC8, 0x3C, 0x23);

    private static readonly Color Green = Color.FromRgb(0x00, 0xE0, 0x79);


    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not GachaStatsItemViewModel item)
        {
            return null;
        }

        var pity = item.Pity;
        var point = 74;
        var guarantee = 90.0;

        if (item.GachaType == 302)
        {
            // 原神武器活动祈愿（GenshinGachaType.WeaponEventWish）
            point = 63;
            guarantee = 80;
        }
        else if (item.GachaType is 3 or 5 or 103)
        {
            // 绝区零音擎 / 邦布频段（ZZZGachaType.WEngineChannel / BangbooChannel / WEngineReverberation）
            point = 65;
            guarantee = 80;
        }

        var color = pity < point ? Green : Red;
        var offset = Math.Clamp(pity / guarantee, 0, 1);

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            Opacity = 0.4,
        };
        brush.GradientStops.Add(new GradientStop(color, 0));
        brush.GradientStops.Add(new GradientStop(color, offset));
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, offset));
        return brush;
    }


    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
