using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;

namespace LunarWard.Converters;

internal partial class TimeSpanToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan time)
        {
            return $"{Math.Floor(time.TotalHours)}h {time.Minutes}m";
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

