using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace LunarWard.Features.GameRecord.StarRail;

internal partial class SimulatedUniverseBuffBgConverter : IValueConverter
{


    private static SolidColorBrush Rank1Brush = new SolidColorBrush(Color.FromArgb(0xFF, 0x68, 0x68, 0x70));
    private static SolidColorBrush Rank2Brush = new SolidColorBrush(Color.FromArgb(0xFF, 0x53, 0x79, 0xB5));
    private static SolidColorBrush Rank3Brush = new SolidColorBrush(Color.FromArgb(0xFF, 0xBC, 0x9B, 0x6E));
    private static SolidColorBrush Rank0Brush = new SolidColorBrush(Colors.Transparent);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (int)value switch
        {
            1 => Rank1Brush,
            2 => Rank2Brush,
            3 => Rank3Brush,
            _ => Rank0Brush,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

}