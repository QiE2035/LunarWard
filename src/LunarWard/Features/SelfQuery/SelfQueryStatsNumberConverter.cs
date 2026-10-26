using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace LunarWard.Features.SelfQuery;

/// <summary>
/// 上游 <c>Starward.Features.SelfQuery.SelfQueryStatsNumberConverter</c>。
/// 平台差异（docs/09）：WinUI <c>IValueConverter</c> 签名 → Avalonia 版（<c>object?</c> + <see cref="CultureInfo"/>）。
/// </summary>
internal partial class SelfQueryStatsNumberConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        long num = (long)value!;
        if (num > 0)
        {
            return $"+{num}";
        }
        if (num < 0)
        {
            return num.ToString();
        }
        return "-";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}



/// <summary>
/// 上游 <c>Starward.Features.SelfQuery.SelfQueryStatsNumberBrushConverter</c>。
/// 平台差异（docs/09）：WinUI 资源键 <c>SystemFillColorSuccessBrush</c>/<c>SystemFillColorCriticalBrush</c>/
/// <c>TextFillColorSecondaryBrush</c> → <c>StarSuccessBrush</c>/<c>StarErrorBrush</c>/<c>StarTextSecondary</c>。
/// </summary>
internal partial class SelfQueryStatsNumberBrushConverter : IValueConverter
{

    private static readonly Brush? GreenBrush = Application.Current?.Resources["StarSuccessBrush"] as Brush;
    private static readonly Brush? RedBrush = Application.Current?.Resources["StarErrorBrush"] as Brush;
    private static readonly Brush? DefaultBrush = Application.Current?.Resources["StarTextSecondary"] as Brush;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        long num = (long)value!;
        if (num > 0)
        {
            return GreenBrush!;
        }
        if (num < 0)
        {
            return RedBrush!;
        }
        return DefaultBrush!;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
