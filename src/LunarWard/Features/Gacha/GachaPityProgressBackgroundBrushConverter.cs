using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Core.Gacha.StarRail;
using LunarWard.Core.Gacha.ZZZ;
using System;
using System.Globalization;

namespace LunarWard.Features.Gacha;

/// <summary>
/// 上游用 WinUI <c>LinearGradientBrush</c>（<c>Windows.Foundation.Point</c> + <c>Windows.UI.Color</c>）；
/// Avalonia 侧换成 <see cref="LinearGradientBrush"/> + <see cref="RelativePoint"/> + <see cref="Color"/>，
/// 渐变端点与颜色值保持上游一致。
/// </summary>
internal partial class GachaPityProgressBackgroundBrushConverter : IValueConverter
{

    private static Color Red = Color.FromArgb(0xFF, 0xC8, 0x3C, 0x23);
    private static Color Green = Color.FromArgb(0xFF, 0x00, 0xE0, 0x79);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GachaLogItemEx item)
        {
            int pity = item.Pity;
            var brush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), Opacity = 0.4 };
            int point = 74;
            double guarantee = 90;
            if (item.GachaType is GenshinGachaType.WeaponEventWish or StarRailGachaType.LightConeEventWarp or StarRailGachaType.LightConeCollaborationWarp)
            {
                point = 63;
                guarantee = 80;
            }
            else if (item.GachaType is ZZZGachaType.WEngineChannel or ZZZGachaType.WEngineReverberation or ZZZGachaType.BangbooChannel)
            {
                point = 65;
                guarantee = 80;
            }
            double offset = pity / guarantee;
            if (pity < point)
            {
                brush.GradientStops.Add(new GradientStop { Color = Green, Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Green, Offset = offset });
                brush.GradientStops.Add(new GradientStop { Color = Colors.Transparent, Offset = offset });
            }
            else
            {
                brush.GradientStops.Add(new GradientStop { Color = Red, Offset = 0 });
                brush.GradientStops.Add(new GradientStop { Color = Red, Offset = offset });
                brush.GradientStops.Add(new GradientStop { Color = Colors.Transparent, Offset = offset });
            }
            return brush;
        }
        return null!;
    }


    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

}
