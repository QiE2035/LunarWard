using Avalonia.Data.Converters;
using Serilog;
using System.Collections;
using System.Globalization;
using System.Reflection;

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

/// <summary>
/// 上游 WinUI 的 <c>{x:Bind local:Page.SomeMethod(Arg)}</c> 函数绑定（含两参数重载）的等价物。
/// </summary>
/// <remarks>
/// Linux: 上游 WinUI 的 x:Bind 支持直接调用页面方法；Avalonia 的绑定表达式不能调用方法，
/// 因此这里用反射调用页面上的 <c>public static</c> 方法（移植时把上游这些方法保持 static）。
/// XAML 写法：<c>{Binding Arg, Converter={StaticResource MethodValue}, ConverterParameter='PageType.Method'}</c>，
/// 两参数方法写成 <c>'PageType.Method|第二个字面量'</c>（例如 <c>'ShiyuDefensePage.ElementWeaknessVisibility|-1'</c>）。
/// </remarks>
public sealed class MethodValueConverter : IValueConverter
{
    private static readonly Dictionary<string, MethodInfo?> Cache = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string spec || spec.Length == 0)
        {
            return null;
        }

        string methodSpec = spec;
        string? extra = null;
        int bar = spec.IndexOf('|');
        if (bar >= 0)
        {
            methodSpec = spec[..bar];
            extra = spec[(bar + 1)..];
        }

        MethodInfo? method = Resolve(methodSpec);
        if (method is null)
        {
            Log.Warning($"MethodValueConverter 找不到方法 '{methodSpec}'");
            return null;
        }

        ParameterInfo[] parameters = method.GetParameters();
        object?[] args;
        if (parameters.Length == 0)
        {
            args = Array.Empty<object?>();
        }
        else if (parameters.Length == 1)
        {
            args = [Coerce(value, parameters[0].ParameterType, culture)];
        }
        else
        {
            args =
            [
                Coerce(value, parameters[0].ParameterType, culture),
                Coerce(extra, parameters[1].ParameterType, culture),
            ];
        }

        try
        {
            return method.Invoke(null, args);
        }
        catch (Exception ex)
        {
            Log.Warning($"MethodValueConverter 调用 '{methodSpec}' 失败：{ex.GetBaseException().Message}");
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static MethodInfo? Resolve(string spec)
    {
        if (Cache.TryGetValue(spec, out MethodInfo? cached))
        {
            return cached;
        }

        int dot = spec.LastIndexOf('.');
        MethodInfo? method = null;
        if (dot > 0)
        {
            string typeName = spec[..dot];
            string methodName = spec[(dot + 1)..];
            Type? type = typeof(MethodValueConverter).Assembly.GetTypes().FirstOrDefault(t =>
                t.Name == typeName && t.Namespace is not null && t.Namespace.StartsWith("LunarWard.Features.GameRecord", StringComparison.Ordinal));
            method = type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        }

        Cache[spec] = method;
        return method;
    }

    private static object? Coerce(object? value, Type targetType, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (type.IsInstanceOfType(value))
        {
            return value;
        }

        if (type == typeof(int) && value is double d)
        {
            return (int)d;
        }

        try
        {
            return System.Convert.ChangeType(value, type, culture);
        }
        catch
        {
            return null;
        }
    }
}
