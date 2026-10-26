using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace LunarWard.Features.GameRecord.StarRail;

internal partial class SimulatedUniverseWorldIconConverter : IValueConverter
{

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return $"ms-appx:///Assets/Image/PicRoguePlanetM{(int)value}.png";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

}
