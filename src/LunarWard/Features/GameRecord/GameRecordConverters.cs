using Avalonia.Data.Converters;
using System.Collections;
using System.Globalization;

namespace LunarWard.Features.GameRecord;

/// <summary>
/// 上游 WinUI 的 <c>{x:Bind linq:Enumerable.Range(0, Count)}</c> 函数绑定的等价物。
/// </summary>
/// <remarks>
/// Avalonia 的绑定表达式不能调用方法，所以把「0..Count-1 的整数序列」做成转换器：
/// XAML 里写 <c>ItemsSource="{Binding Count, Converter={StaticResource RangeConverter}}"</c>，
/// 与上游 <c>Enumerable.Range(0, Count)</c> 生成相同数量的元素。
/// </remarks>
public sealed class RangeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value switch
        {
            int i => i,
            double d => (int)d,
            _ => 0,
        };
        if (count <= 0)
        {
            return Array.Empty<int>();
        }

        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = i;
        }
        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}


/// <summary>
/// 上游 <c>{x:Bind sfgr:AvatarRankHelper.RankToVisibility(Rank)}</c> 的等价物。
/// </summary>
/// <remarks>
/// Avalonia 没有函数绑定，且 <c>Visibility</c> 被 <c>bool</c> + <c>IsVisible</c> 取代，
/// 所以这里直接返回 <c>rank &gt; 0</c>（与 <see cref="AvatarRankHelper.RankToVisibility"/> 同语义）。
/// </remarks>
public sealed class RankToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int rank && rank > 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
