using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace LunarWard.Features.GameRecord;

internal partial class ColorToSolidColorBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new SolidColorBrush((Color)value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
