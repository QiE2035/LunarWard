using System.Globalization;
using System.Text.Json.Nodes;
using LunarWard.Launch;

namespace LunarWard.Services;

/// <summary>
/// 游戏设置页的注册表语义（对应 Starward <c>Features/GameSetting/GameSettingService.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 键路径与值名逐条照抄 Starward（见 <c>Starward.Core/GameRegistry.cs</c> 与
/// <c>GameSettingService.cs</c>），只把 <c>Microsoft.Win32.Registry</c> 换成
/// <see cref="WineRegistry"/>（同一个 Wine prefix 的 <c>wine reg</c>）。
/// </para>
/// <para>
/// LunarWard 只支持原神/绝区零：原神的分辨率/全屏/语音、绝区零的分辨率/全屏。
/// 星铁的帧率与 HDR 不在支持范围内（HDR 在 Linux/Wine 下也没有等价物）。
/// </para>
/// </remarks>
public static class GameSettingRegistry
{
    // 原神（Starward GameSettingService 的常量，原样照抄）
    private const string GenshinFullscreen = "Screenmanager Is Fullscreen mode_h3981298716";
    private const string GenshinWidth = "Screenmanager Resolution Width_h182942802";
    private const string GenshinHeight = "Screenmanager Resolution Height_h2627697771";
    private const string GenshinGeneralData = "GENERAL_DATA_h2389025596";

    // 绝区零（Starward 用 1 = 全屏，3 = 窗口）
    private const string ZzzFullscreen = "Screenmanager Fullscreen mode_h3630240806";
    private const string ZzzWidth = "Screenmanager Resolution Width_h182942802";
    private const string ZzzHeight = "Screenmanager Resolution Height_h2627697771";

    private const int ZzzFullscreenMode = 1;
    private const int ZzzWindowMode = 3;


    /// <summary>原神 / 绝区零的注册表键（Starward <c>GameBiz.GetGameRegistryKey()</c>）。</summary>
    public static string? RegistryKeyFor(string game, string edition)
    {
        var global = string.Equals(edition, "Global", StringComparison.OrdinalIgnoreCase);
        return game.ToLowerInvariant() switch
        {
            "genshin" => global
                ? @"HKEY_CURRENT_USER\Software\miHoYo\Genshin Impact"
                : @"HKEY_CURRENT_USER\Software\miHoYo\原神",
            "zzz" => global
                ? @"HKEY_CURRENT_USER\Software\miHoYo\ZenlessZoneZero"
                : @"HKEY_CURRENT_USER\Software\miHoYo\绝区零",
            _ => null,
        };
    }


    /// <summary>游戏是否支持语音语言设置（Starward：原神 / 星铁）。</summary>
    public static bool SupportsVoiceLanguage(string game)
        => string.Equals(game, "genshin", StringComparison.OrdinalIgnoreCase);


    /// <summary>读分辨率/全屏。注册表里没有时返回 <c>null</c>（页面用默认值）。</summary>
    public static async Task<(bool IsFullScreen, int Width, int Height)?> ReadResolutionAsync(
        WineRegistry registry,
        string wineBinary,
        string prefix,
        string game,
        string edition,
        CancellationToken ct = default)
    {
        if (RegistryKeyFor(game, edition) is not { } key)
        {
            return null;
        }

        var width = await registry.ReadDwordAsync(wineBinary, prefix, key, WidthValueFor(game), ct).ConfigureAwait(false);
        var height = await registry.ReadDwordAsync(wineBinary, prefix, key, HeightValueFor(game), ct).ConfigureAwait(false);
        if (width is not > 0 || height is not > 0)
        {
            return null;
        }

        var fullscreen = await registry.ReadDwordAsync(wineBinary, prefix, key, FullscreenValueFor(game), ct).ConfigureAwait(false);
        var isFull = game.ToLowerInvariant() == "zzz"
            ? fullscreen is not null && fullscreen != ZzzWindowMode
            : fullscreen is not null && fullscreen != 0;
        return (isFull, width.Value, height.Value);
    }


    /// <summary>写分辨率/全屏（值名与写法照 Starward <c>SetGameResolutionSetting</c>）。</summary>
    public static async Task<bool> WriteResolutionAsync(
        WineRegistry registry,
        string wineBinary,
        string prefix,
        string game,
        string edition,
        bool isFullScreen,
        int width,
        int height,
        CancellationToken ct = default)
    {
        if (RegistryKeyFor(game, edition) is not { } key)
        {
            return false;
        }

        var fullscreenValue = game.ToLowerInvariant() == "zzz"
            ? (isFullScreen ? ZzzFullscreenMode : ZzzWindowMode)
            : (isFullScreen ? 1 : 0);

        var ok = await registry.WriteDwordAsync(wineBinary, prefix, key, FullscreenValueFor(game), fullscreenValue, ct).ConfigureAwait(false);
        ok &= await registry.WriteDwordAsync(wineBinary, prefix, key, WidthValueFor(game), width, ct).ConfigureAwait(false);
        ok &= await registry.WriteDwordAsync(wineBinary, prefix, key, HeightValueFor(game), height, ct).ConfigureAwait(false);
        return ok;
    }


    /// <summary>读原神语音语言索引（0 汉 / 1 英 / 2 日 / 3 韩）。读不到返回 <c>null</c>。</summary>
    public static async Task<int?> ReadVoiceLanguageAsync(
        WineRegistry registry,
        string wineBinary,
        string prefix,
        string game,
        string edition,
        CancellationToken ct = default)
    {
        if (!SupportsVoiceLanguage(game) || RegistryKeyFor(game, edition) is not { } key)
        {
            return null;
        }

        var data = await registry.ReadBinaryAsync(wineBinary, prefix, key, GenshinGeneralData, ct).ConfigureAwait(false);
        var json = WineRegistry.DecodeUtf8Z(data);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(json);
            return node?["deviceVoiceLanguageType"]?.GetValue<int>();
        }
        catch
        {
            return null;
        }
    }


    /// <summary>
    /// 写原神语音语言索引。只改 JSON 里的 <c>deviceVoiceLanguageType</c>，其余字段原样保留；
    /// 原值不存在时按 Starward 的默认结构新建。
    /// </summary>
    public static async Task<bool> WriteVoiceLanguageAsync(
        WineRegistry registry,
        string wineBinary,
        string prefix,
        string game,
        string edition,
        int languageIndex,
        CancellationToken ct = default)
    {
        if (!SupportsVoiceLanguage(game) || RegistryKeyFor(game, edition) is not { } key)
        {
            return false;
        }

        JsonObject node;
        var data = await registry.ReadBinaryAsync(wineBinary, prefix, key, GenshinGeneralData, ct).ConfigureAwait(false);
        var json = WineRegistry.DecodeUtf8Z(data);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                node = JsonNode.Parse(json) as JsonObject ?? [];
            }
            catch
            {
                node = [];
            }
        }
        else
        {
            node = [];
        }

        node["deviceVoiceLanguageType"] = languageIndex;
        var payload = node.ToJsonString();
        return await registry.WriteBinaryAsync(
            wineBinary,
            prefix,
            key,
            GenshinGeneralData,
            WineRegistry.EncodeUtf8Z(payload),
            ct).ConfigureAwait(false);
    }


    /// <summary>常见分辨率列表（Starward <c>Resolutions</c> 的前半段，按 16:9 与 16:10 混合）。</summary>
    public static IReadOnlyList<(int Width, int Height)> CommonResolutions { get; } =
    [
        (3840, 2160),
        (2560, 1600),
        (2560, 1440),
        (2048, 1536),
        (1920, 1440),
        (1920, 1200),
        (1920, 1080),
        (1680, 1050),
        (1600, 1200),
        (1440, 900),
        (1280, 960),
        (1280, 800),
        (1280, 720),
        (1152, 864),
        (1024, 768),
        (800, 600),
        (640, 480),
    ];


    public static string FormatResolution(int width, int height)
        => $"{width.ToString(CultureInfo.InvariantCulture)} × {height.ToString(CultureInfo.InvariantCulture)}";


    private static string WidthValueFor(string game) => game.ToLowerInvariant() == "zzz" ? ZzzWidth : GenshinWidth;

    private static string HeightValueFor(string game) => game.ToLowerInvariant() == "zzz" ? ZzzHeight : GenshinHeight;

    private static string FullscreenValueFor(string game) => game.ToLowerInvariant() == "zzz" ? ZzzFullscreen : GenshinFullscreen;
}
