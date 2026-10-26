using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;

namespace LunarWard.Converters;

public partial class WhiteSpaceStringToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

