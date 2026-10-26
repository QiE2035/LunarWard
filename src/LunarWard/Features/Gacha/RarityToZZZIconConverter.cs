using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace LunarWard.Features.Gacha;

internal partial class RarityToZZZIconConverter : IValueConverter
{

    private const string Rarity2Background = "ms-appx:///Assets/Image/B_Level_S.png";
    private const string Rarity3Background = "ms-appx:///Assets/Image/A_Level_S.png";
    private const string Rarity4Background = "ms-appx:///Assets/Image/S_Level_S.png";
    private const string TransparentBackground = "ms-appx:///Assets/Image/Transparent.png";


    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (int)value! switch
        {
            2 => Rarity2Background,
            3 => Rarity3Background,
            4 => Rarity4Background,
            _ => TransparentBackground,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
