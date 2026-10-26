using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;

namespace LunarWard.Features.PlayTime.Charts;

/// <summary>
/// 图表共享的资源辅助方法，对应上游 <c>Starward.Features.PlayTime.ChartHelpers</c>。
/// </summary>
/// <remarks>
/// 上游键名是 WinUI Fluent 资源（<c>TextFillColorSecondaryBrush</c> 等），本仓库不引 Fluent 资源字典，
/// 因此这里把它们映射到 <c>Styles/Starward.axaml</c> 里的 <c>Star*</c> 键；映射表之外的键仍然抛
/// <see cref="KeyNotFoundException"/>（与上游一致，不做静默兜底）。详见 docs/09-LINUX-DIVERGENCES.md「十六、Features/PlayTime」。
/// </remarks>
internal static class ChartHelpers
{

    /// <summary>
    /// WinUI Fluent 资源键 → 本仓库样式键（<c>src/LunarWard/Styles/Starward.axaml</c>）。
    /// </summary>
    private static readonly Dictionary<string, string> WinUIKeyAliases = new()
    {
        ["TextFillColorSecondaryBrush"] = "StarTextSecondary",
        ["DividerStrokeColorDefaultBrush"] = "StarDividerBrush",
        ["AccentFillColorDefaultBrush"] = "StarAccentBrush",
        ["CardBackgroundFillColorDefaultBrush"] = "StarCardBackgroundBrush",
        ["CustomOverlayAcrylicBrush"] = "StarOverlayAcrylicBrush",
    };


    /// <summary>
    /// 获取主题资源（先按上游键名找，找不到再走别名映射）。
    /// </summary>
    public static T GetResource<T>(string key) where T : class
    {
        if (TryGetResource(key, out T? value))
        {
            return value!;
        }
        if (WinUIKeyAliases.TryGetValue(key, out string? alias) && TryGetResource(alias, out value))
        {
            return value!;
        }
        throw new KeyNotFoundException($"Theme resource '{key}' not found.");
    }



    private static bool TryGetResource<T>(string key, out T? value) where T : class
    {
        if (Application.Current is { } app && app.TryFindResource(key, out object? resource) && resource is T t)
        {
            value = t;
            return true;
        }
        value = null;
        return false;
    }

}
