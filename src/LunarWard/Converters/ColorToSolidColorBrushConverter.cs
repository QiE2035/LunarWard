using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;

namespace LunarWard.Converters;

using Avalonia.Media;

internal partial class ColorToSolidColorBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new SolidColorBrush((Color)value!);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
