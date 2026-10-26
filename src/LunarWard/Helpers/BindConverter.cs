namespace LunarWard.Helpers;

/// <summary>
/// 上游 Helpers/BindConverter.cs 的等价物：WinUI 的 x:Bind 函数绑定返回 <c>Visibility</c>，
/// Avalonia 的 <c>IsVisible</c> 是 bool，因此这里返回 bool，语义不变。
/// </summary>
public static class BindConverter
{
    public static bool BoolToVisibility(bool value) => value;

    public static bool BoolToVisibilityReversed(bool value) => !value;

    public static bool ObjectToVisibility(object? value) => value is not null;

    public static bool ObjectToVisibilityReversed(object? value) => value is null;
}
