using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System;
using System.Globalization;

namespace LunarWard.Features.GameRecord.ZZZ;

internal partial class ZZZRarityToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var img = value switch
        {
            "S+" => "ms-appx:///Assets/Image/rating-s_plus.c6cbdc49.png",
            "S" => "ms-appx:///Assets/Image/S_Level_S.png",
            "A" => "ms-appx:///Assets/Image/A_Level_S.png",
            "B" => "ms-appx:///Assets/Image/B_Level_S.png",
            _ => "ms-appx:///Assets/Image/Transparent.png",
        };
        return new Bitmap(new Uri(img));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
