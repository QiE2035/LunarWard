using Avalonia.Data.Converters;
using System;
using System.Globalization;


namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ZoomFactorPercentConverter</c>（WinUI <c>IValueConverter</c>）的 Avalonia 移植。
/// 只是把缩放率格式化成百分比文本。
/// </summary>
public partial class ZoomFactorPercentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ((double)value!).ToString("P0");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
