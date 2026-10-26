using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;

namespace LunarWard.Converters;

public partial class BoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true;
}


public partial class BoolToVisibilityReversedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is false;
}
