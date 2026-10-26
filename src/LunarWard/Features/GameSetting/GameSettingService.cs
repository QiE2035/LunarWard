using LunarWard.Core;
using LunarWard.Features.GameLauncher;
using LunarWard.Platform;
using LunarWard.Services;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.GameSetting;

/// <summary>
/// 游戏内画质/分辨率/语音设置。<b>移植自</b>
/// <c>Starward/src/Starward/Features/GameSetting/GameSettingService.cs</c>。
/// </summary>
/// <remarks>
/// <para>
/// <b>平台替换</b>：上游全部用 <c>Microsoft.Win32.Registry.GetValue/SetValue</c>；
/// Linux 下同样的值在 Wine prefix 里，经 <see cref="WineRegistry"/>（<c>wine reg query/add</c>）读写。
/// 由于是进程调用，方法全部改为 <c>Async</c>（唯一签名差异，方法名/语义/键名/JSON 结构逐条对齐）。
/// </para>
/// <para>
/// 键路径来自 <see cref="GameBiz.GetGameRegistryKey"/>（<c>HKEY_CURRENT_USER\Software\miHoYo\原神</c> 等），
/// wine reg 接受同样的写法。见 <c>docs/09-LINUX-DIVERGENCES.md</c>。
/// </para>
/// </remarks>
internal static class GameSettingService
{


    private const string GENERAL_DATA_h2389025596 = "GENERAL_DATA_h2389025596";
    private const string Screenmanager_Is_Fullscreen_mode_h3981298716 = "Screenmanager Is Fullscreen mode_h3981298716";
    private const string Screenmanager_Resolution_Height_h2627697771 = "Screenmanager Resolution Height_h2627697771";
    private const string Screenmanager_Resolution_Width_h182942802 = "Screenmanager Resolution Width_h182942802";


    private const string AudioSettings_BGMVolume_h240914409 = "AudioSettings_BGMVolume_h240914409";
    private const string AudioSettings_MasterVolume_h1622207037 = "AudioSettings_MasterVolume_h1622207037";
    private const string AudioSettings_SFXVolume_h2753520268 = "AudioSettings_SFXVolume_h2753520268";
    private const string AudioSettings_VOVolume_h805685304 = "AudioSettings_VOVolume_h805685304";
    private const string GraphicsSettings_GraphicsQuality_h523255858 = "GraphicsSettings_GraphicsQuality_h523255858";
    private const string GraphicsSettings_Model_h2986158309 = "GraphicsSettings_Model_h2986158309";
    private const string GraphicsSettings_PCResolution_h431323223 = "GraphicsSettings_PCResolution_h431323223";
    // cn en jp kr
    private const string LanguageSettings_LocalAudioLanguage_h882585060 = "LanguageSettings_LocalAudioLanguage_h882585060";
    private const string LanguageSettings_LocalTextLanguage_h2764291023 = "LanguageSettings_LocalTextLanguage_h2764291023";
    // 1 full, 3 window
    private const string Screenmanager_Fullscreen_mode_h3630240806 = "Screenmanager Fullscreen mode_h3630240806";


    private const string GENERAL_DATA_V2_ScreenSettingData_h1916288658 = "GENERAL_DATA_V2_ScreenSettingData_h1916288658";

    private const string WINDOWS_HDR_ON_h3132281285 = "WINDOWS_HDR_ON_h3132281285";



    private static readonly Lazy<WineRegistry> _registry = new(() => new WineRegistry(UnixProcessLauncher.Default, AppServices.Current.Log));


    private static string Prefix => GamePrefixContext.CurrentPrefix!;


    private static string WineBinary => GamePrefixContext.CurrentWineBinary ?? "wine";



    public static async Task<GraphicsSettings_PCResolution_h431323223?> GetGameResolutionSettingAsync(GameBiz biz, CancellationToken cancellationToken = default)
    {
        var keyPath = biz.GetGameRegistryKey();
        if (biz.ToGame().Value is GameBiz.bh3 or GameBiz.hkrpg)
        {
            var keyName = biz.ToGame().Value switch
            {
                GameBiz.bh3 => GENERAL_DATA_V2_ScreenSettingData_h1916288658,
                GameBiz.hkrpg => GraphicsSettings_PCResolution_h431323223,
                _ => ""
            };
            var data = await _registry.Value.ReadBinaryAsync(WineBinary, Prefix, keyPath, keyName, cancellationToken);
            if (data is not null)
            {
                var str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                return JsonSerializer.Deserialize<GraphicsSettings_PCResolution_h431323223>(str);
            }
        }
        if (biz.ToGame() == GameBiz.hk4e)
        {
            var fullScreen = (await _registry.Value.ReadDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Is_Fullscreen_mode_h3981298716, cancellationToken) ?? 0) != 0;
            var width = await _registry.Value.ReadDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Width_h182942802, cancellationToken) ?? 0;
            var height = await _registry.Value.ReadDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Height_h2627697771, cancellationToken) ?? 0;
            if (width * height > 0)
            {
                return new GraphicsSettings_PCResolution_h431323223 { Width = width, Height = height, IsFullScreen = fullScreen };
            }
        }
        if (biz.ToGame() == GameBiz.nap)
        {
            var fullScreen = (await _registry.Value.ReadDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Fullscreen_mode_h3630240806, cancellationToken) ?? 0) != 3;
            var width = await _registry.Value.ReadDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Width_h182942802, cancellationToken) ?? 0;
            var height = await _registry.Value.ReadDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Height_h2627697771, cancellationToken) ?? 0;
            if (width * height > 0)
            {
                return new GraphicsSettings_PCResolution_h431323223 { Width = width, Height = height, IsFullScreen = fullScreen };
            }
        }
        return null;
    }



    public static async Task SetGameResolutionSettingAsync(GameBiz biz, GraphicsSettings_PCResolution_h431323223 model, CancellationToken cancellationToken = default)
    {
        var keyPath = biz.GetGameRegistryKey();
        if (biz.ToGame() == GameBiz.bh3)
        {
            var str = JsonSerializer.Serialize(model) + "\0";
            await _registry.Value.WriteBinaryAsync(WineBinary, Prefix, keyPath, GENERAL_DATA_V2_ScreenSettingData_h1916288658, Encoding.UTF8.GetBytes(str), cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Is_Fullscreen_mode_h3981298716, model.IsFullScreen ? 1 : 0, cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Width_h182942802, model.Width, cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Height_h2627697771, model.Height, cancellationToken);
        }
        if (biz.ToGame() == GameBiz.hkrpg)
        {
            var str = JsonSerializer.Serialize(model) + "\0";
            await _registry.Value.WriteBinaryAsync(WineBinary, Prefix, keyPath, GraphicsSettings_PCResolution_h431323223, Encoding.UTF8.GetBytes(str), cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Fullscreen_mode_h3630240806, model.IsFullScreen ? 1 : 3, cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Width_h182942802, model.Width, cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Height_h2627697771, model.Height, cancellationToken);
        }
        if (biz.ToGame() == GameBiz.hk4e)
        {
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Is_Fullscreen_mode_h3981298716, model.IsFullScreen ? 1 : 0, cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Width_h182942802, model.Width, cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Height_h2627697771, model.Height, cancellationToken);
        }
        if (biz.ToGame() == GameBiz.nap)
        {
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Fullscreen_mode_h3630240806, model.IsFullScreen ? 1 : 3, cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Width_h182942802, model.Width, cancellationToken);
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, keyPath, Screenmanager_Resolution_Height_h2627697771, model.Height, cancellationToken);
        }
    }




    public static async Task<int?> GetGameVoiceLanguageSettingAsync(GameBiz biz, CancellationToken cancellationToken = default)
    {
        var keyPath = biz.GetGameRegistryKey();
        if (biz.ToGame() == GameBiz.hk4e)
        {
            var data = await _registry.Value.ReadBinaryAsync(WineBinary, Prefix, keyPath, GENERAL_DATA_h2389025596, cancellationToken);
            if (data is not null)
            {
                var str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                var node = JsonNode.Parse(str);
                return node?["deviceVoiceLanguageType"]?.GetValue<int>();
            }
        }
        if (biz.ToGame() == GameBiz.hkrpg)
        {
            var data = await _registry.Value.ReadBinaryAsync(WineBinary, Prefix, keyPath, LanguageSettings_LocalAudioLanguage_h882585060, cancellationToken);
            if (data is not null)
            {
                var str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                return str switch
                {
                    "cn" => 0,
                    "en" => 1,
                    "jp" => 2,
                    "kr" => 3,
                    _ => null,
                };
            }
        }
        return null;
    }


    public static async Task SetGameVoiceLanguageSettingAsync(GameBiz biz, int lang, CancellationToken cancellationToken = default)
    {
        var keyPath = biz.GetGameRegistryKey();
        if (biz.Game is GameBiz.hk4e)
        {
            var data = await _registry.Value.ReadBinaryAsync(WineBinary, Prefix, keyPath, GENERAL_DATA_h2389025596, cancellationToken);
            JsonNode? node = null;
            if (data is not null)
            {
                var str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                node = JsonNode.Parse(str);
                node?["deviceVoiceLanguageType"] = lang;
            }
            string value = $"{node?.ToJsonString() ?? ($$"""{"deviceVoiceLanguageType":{{lang}}}""")}\0";
            await _registry.Value.WriteBinaryAsync(WineBinary, Prefix, keyPath, GENERAL_DATA_h2389025596, Encoding.UTF8.GetBytes(value), cancellationToken);
        }
        if (biz.Game is GameBiz.hkrpg)
        {
            var str = lang switch
            {
                0 => "cn\0",
                1 => "en\0",
                2 => "jp\0",
                3 => "kr\0",
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(str))
            {
                await _registry.Value.WriteBinaryAsync(WineBinary, Prefix, keyPath, LanguageSettings_LocalAudioLanguage_h882585060, Encoding.UTF8.GetBytes(str), cancellationToken);
            }
        }
    }



    public static async Task<int> GetStarRailFPSIndexAsync(GameBiz biz, CancellationToken cancellationToken = default)
    {
        if (biz.Game is GameBiz.hkrpg)
        {
            string key = biz.GetGameRegistryKey();
            byte[]? data = await _registry.Value.ReadBinaryAsync(WineBinary, Prefix, key, GraphicsSettings_Model_h2986158309, cancellationToken);
            if (data is not null)
            {
                string str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                JsonNode? node = JsonNode.Parse(str);
                if (node is not null)
                {
                    int fps = node["FPS"]?.GetValue<int>() ?? 60;
                    return fps switch
                    {
                        30 => 0,
                        120 => 2,
                        _ => 1,
                    };
                }
            }
        }
        return 1;
    }


    public static async Task SetStarRailFPSIndexAsync(GameBiz biz, int fpsIndex, CancellationToken cancellationToken = default)
    {
        if (biz.Game is GameBiz.hkrpg)
        {
            int fps = fpsIndex switch
            {
                0 => 30,
                1 => 60,
                2 => 120,
                _ => 60,
            };
            string key = biz.GetGameRegistryKey();
            JsonNode? node = null;
            byte[]? data = await _registry.Value.ReadBinaryAsync(WineBinary, Prefix, key, GraphicsSettings_Model_h2986158309, cancellationToken);
            if (data is not null)
            {
                string str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                node = JsonNode.Parse(str);
                node?["FPS"] = fps;
            }

            string? value = node?.ToJsonString();
            if (string.IsNullOrWhiteSpace(value))
            {
                value = $$"""
                    {"FPS":{{fps}},"EnableVSync":false,"RenderScale":1.0,"ResolutionQuality":3,"ShadowQuality":3,"LightQuality":3,"CharacterQuality":3,"EnvDetailQuality":3,"ReflectionQuality":3,"SFXQuality":3,"BloomQuality":3,"AAMode":1,"EnableMetalFXSU":false,"EnableHalfResTransparent":false,"EnableSelfShadow":1,"DlssQuality":0}
                    """;
            }
            value += "\0";
            // 画质预设设置为0（自定义）时，帧率设置方能生效
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, key, GraphicsSettings_GraphicsQuality_h523255858, 0, cancellationToken);
            await _registry.Value.WriteBinaryAsync(WineBinary, Prefix, key, GraphicsSettings_Model_h2986158309, Encoding.UTF8.GetBytes(value), cancellationToken);
        }
    }



    public static async Task SetGenshinEnableHDRAsync(GameBiz biz, bool enableHDR, CancellationToken cancellationToken = default)
    {
        if (biz.Game is GameBiz.hk4e)
        {
            string key = biz.GetGameRegistryKey();
            await _registry.Value.WriteDwordAsync(WineBinary, Prefix, key, WINDOWS_HDR_ON_h3132281285, enableHDR ? 1 : 0, cancellationToken);
        }
    }





    public static async Task<(int MaxLuminance, int SceneLuminance, int UILuminance)> GetGenshinHDRLuminanceAsync(GameBiz biz, CancellationToken cancellationToken = default)
    {
        int max = 1000, scene = 300, ui = 350;
        if (biz.Game is GameBiz.hk4e)
        {
            string key = biz.GetGameRegistryKey();
            byte[]? data = await _registry.Value.ReadBinaryAsync(WineBinary, Prefix, key, GENERAL_DATA_h2389025596, cancellationToken);
            if (data is not null)
            {
                string str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                JsonNode? node = JsonNode.Parse(str);
                if (node is not null)
                {
                    max = (int)(node["maxLuminosity"]?.GetValue<float>() ?? 1000);
                    scene = (int)(node["scenePaperWhite"]?.GetValue<float>() ?? 300);
                    ui = (int)(node["uiPaperWhite"]?.GetValue<float>() ?? 350);
                }
            }
        }
        max = Math.Clamp(max, 300, 2000);
        scene = Math.Clamp(scene, 100, 500);
        ui = Math.Clamp(ui, 150, 550);
        return (max, scene, ui);
    }



    public static async Task SetGenshinHDRLuminanceAsync(GameBiz biz, int maxLuminance, int sceneLuminance, int uiLuminance, CancellationToken cancellationToken = default)
    {
        maxLuminance = Math.Clamp(maxLuminance, 300, 2000);
        sceneLuminance = Math.Clamp(sceneLuminance, 100, 500);
        uiLuminance = Math.Clamp(uiLuminance, 150, 550);
        if (biz.Game is GameBiz.hk4e)
        {
            string key = biz.GetGameRegistryKey();
            byte[]? data = await _registry.Value.ReadBinaryAsync(WineBinary, Prefix, key, GENERAL_DATA_h2389025596, cancellationToken);
            JsonNode? node = null;
            if (data is not null)
            {
                string str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                node = JsonNode.Parse(str);
                node?["maxLuminosity"] = maxLuminance;
                node?["scenePaperWhite"] = sceneLuminance;
                node?["uiPaperWhite"] = uiLuminance;
            }
            string value = $"{node?.ToJsonString() ?? ($$"""{"maxLuminosity":{{maxLuminance}},"scenePaperWhite":{{sceneLuminance}},"uiPaperWhite":{{uiLuminance}}""")}\0";
            await _registry.Value.WriteBinaryAsync(WineBinary, Prefix, key, GENERAL_DATA_h2389025596, Encoding.UTF8.GetBytes(value), cancellationToken);
        }
    }





}
