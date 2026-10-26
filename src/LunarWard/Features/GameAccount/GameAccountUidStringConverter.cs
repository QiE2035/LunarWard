using System.Globalization;
using Avalonia.Data.Converters;

namespace LunarWard.Features.GameAccount;

/// <summary>
/// UID 的双向转换：0 显示空串。<b>原样搬自</b>
/// <c>Starward/src/Starward/Features/GameAccount/GameAccountUidStringConverter.cs</c>
/// （只把 <c>Microsoft.UI.Xaml.Data.IValueConverter</c> 换成 Avalonia 的同名接口）。
/// </summary>
public partial class GameAccountUidStringConverter : IValueConverter
{

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            long uid when uid > 0 => uid.ToString(),
            _ => string.Empty
        };
    }


    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (long.TryParse(value as string, out long uid))
        {
            return uid;
        }
        else
        {
            return 0L;
        }
    }

}
