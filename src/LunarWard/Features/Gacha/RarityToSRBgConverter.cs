using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace LunarWard.Features.Gacha;

internal partial class RarityToSRBgConverter : IValueConverter
{

    private const string Rarity1Background = "ms-appx:///Assets/Image/FrameIconRarity01.png";
    private const string Rarity2Background = "ms-appx:///Assets/Image/FrameIconRarity02.png";
    private const string Rarity3Background = "ms-appx:///Assets/Image/FrameIconRarity03.png";
    private const string Rarity4Background = "ms-appx:///Assets/Image/FrameIconRarity04.png";
    private const string Rarity5Background = "ms-appx:///Assets/Image/FrameIconRarity05.png";
    private const string TransparentBackground = "ms-appx:///Assets/Image/Transparent.png";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (int)value! switch
        {
            1 => Rarity1Background,
            2 => Rarity2Background,
            3 => Rarity3Background,
            4 => Rarity4Background,
            5 => Rarity5Background,
            _ => TransparentBackground,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

}
