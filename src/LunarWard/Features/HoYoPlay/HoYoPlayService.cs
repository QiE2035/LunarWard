using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.HoYoPlay;

/// <summary>
/// HoYoPlay 接口的缓存层。<b>原样搬自</b>
/// <c>Starward/src/Starward/Features/HoYoPlay/HoYoPlayService.cs</c>。
/// </summary>
public class HoYoPlayService
{


    private readonly ILogger<HoYoPlayService> _logger;

    private readonly HoYoPlayClient _client;

    private readonly HttpClient _httpClient;

    private readonly IMemoryCache _memoryCache;


    public HoYoPlayService(ILogger<HoYoPlayService> logger, HoYoPlayClient client, HttpClient httpClient, IMemoryCache memoryCache)
    {
        _logger = logger;
        _client = client;
        _httpClient = httpClient;
        _memoryCache = memoryCache;
    }





    public async Task<GameInfo> GetGameInfoAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameInfo)}_{gameId.Id}", out GameInfo? info))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await _client.GetGameInfoAsync(LauncherId.FromGameId(gameId)!, lang, cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameInfo)}_{item.Id}", item, TimeSpan.FromMinutes(10));
            }
            info = list.FirstOrDefault(x => x == gameId);
        }
        return info!;
    }



    public async Task<List<GameInfo>> UpdateGameInfoListAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache is MemoryCache cache)
        {
            cache.Clear();
        }
        List<GameInfo> infos = new List<GameInfo>();
        string lang = CultureInfo.CurrentUICulture.Name;
        if (LanguageUtil.FilterLanguage(lang) is "zh-cn")
        {
            infos.AddRange(await _client.GetGameInfoAsync(LauncherId.ChinaOfficial, lang, cancellationToken));
            infos.AddRange(await _client.GetGameInfoAsync(LauncherId.GlobalOfficial, lang, cancellationToken));
        }
        else
        {
            infos.AddRange(await _client.GetGameInfoAsync(LauncherId.GlobalOfficial, lang, cancellationToken));
            infos.AddRange(await _client.GetGameInfoAsync(LauncherId.ChinaOfficial, lang, cancellationToken));
        }
        foreach ((GameBiz _, string launcherId) in LauncherId.GetBilibiliLaunchers())
        {
            infos.AddRange(await _client.GetGameInfoAsync(launcherId, lang, cancellationToken));
        }
        foreach (var item in infos)
        {
            _memoryCache.Set($"{nameof(GameInfo)}_{item.Id}", item, TimeSpan.FromMinutes(10));
        }
        string json = JsonSerializer.Serialize(infos);
        AppConfig.CachedGameInfo = json;
        _ = DownloadGameVersionPosterAsync(infos);
        return infos;
    }



    private async Task DownloadGameVersionPosterAsync(List<GameInfo> infos)
    {
        try
        {
            List<string> urls = new();
            foreach (var info in infos)
            {
                if (!string.IsNullOrWhiteSpace(info.Display.Background?.Url))
                {
                    urls.Add(info.Display.Background.Url);
                }
            }
            if (AppConfig.CacheFolder is not null)
            {
                string bg = Path.Combine(AppConfig.CacheFolder, "bg");
                Directory.CreateDirectory(bg);
                await Parallel.ForEachAsync(infos, async (info, _) =>
                {
                    if (string.IsNullOrWhiteSpace(info.Display.Background?.Url))
                    {
                        return;
                    }
                    string url = info.Display.Background.Url;
                    try
                    {
                        string name = Path.GetFileName(url);
                        string path = Path.Combine(bg, name);
                        if (!File.Exists(path))
                        {
                            byte[] bytes = await _httpClient.GetByteArrayAsync(url);
                            await File.WriteAllBytesAsync(path, bytes);
                        }
                        AppConfig.SetVersionPoster(info.GameBiz, name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Download image: {url}", url);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(DownloadGameVersionPosterAsync));
        }
    }



    public async Task<GameBackgroundInfo> GetGameBackgroundAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameBackgroundInfo)}_{gameId.Id}", out GameBackgroundInfo? background))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await _client.GetGameBackgroundAsync(LauncherId.FromGameId(gameId)!, lang, cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameBackgroundInfo)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            background = list.FirstOrDefault(x => x.GameId == gameId);
        }
        return background!;
    }



    public async Task<GameContent> GetGameContentAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameContent)}_{gameId.Id}", out GameContent? content))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            content = await _client.GetGameContentAsync(LauncherId.FromGameId(gameId)!, lang, gameId, cancellationToken);
            _memoryCache.Set($"{nameof(GameContent)}_{content.GameId.Id}", content, TimeSpan.FromMinutes(1));
        }
        return content!;
    }



    public async Task<GamePackage> GetGamePackageAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GamePackage)}_{gameId.Id}", out GamePackage? package))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await _client.GetGamePackageAsync(LauncherId.FromGameId(gameId)!, lang, cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GamePackage)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            package = list.FirstOrDefault(x => x.GameId == gameId);
        }
        return package!;
    }



    public async Task<GameConfig?> GetGameConfigAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameConfig)}_{gameId.Id}", out GameConfig? config))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await _client.GetGameConfigAsync(LauncherId.FromGameId(gameId)!, lang, cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameConfig)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            config = list.FirstOrDefault(x => x.GameId == gameId);
        }
        // 仅星穹铁道强制使用 Chunk 作为默认下载模式
        if (config is not null && config.GameId.GameBiz.Value is GameBiz.hkrpg)
        {
            config.DefaultDownloadMode = DownloadMode.DOWNLOAD_MODE_CHUNK;
        }
        return config;
    }



    public async Task<List<GameDeprecatedFile>> GetGameDeprecatedFilesAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        var launcherId = LauncherId.FromGameId(gameId);
        if (launcherId is not null)
        {
            var fileConfig = await _client.GetGameDeprecatedFileConfigAsync(launcherId, "en-us", gameId, cancellationToken);
            if (fileConfig != null)
            {
                return fileConfig.DeprecatedFiles;
            }
        }
        return [];
    }



    public async Task<GameChannelSDK?> GetGameChannelSDKAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameChannelSDK)}_{gameId.Id}", out GameChannelSDK? sdk))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await _client.GetGameChannelSDKAsync(LauncherId.FromGameId(gameId)!, lang, cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameChannelSDK)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            sdk = list.FirstOrDefault(x => x.GameId == gameId);
        }
        return sdk;
    }



    public async Task<GameBranch?> GetGameBranchAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameBranch)}_{gameId.Id}", out GameBranch? branch))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await _client.GetGameBranchAsync(LauncherId.FromGameId(gameId)!, lang, cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameBranch)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            branch = list.FirstOrDefault(x => x.GameId == gameId);
        }
        return branch;
    }




    public async Task<GameSophonChunkBuild?> GetGameSophonChunkBuildAsync(GameBranch gameBranch, GameBranchPackage gameBranchPackage, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameSophonChunkBuild)}_{gameBranchPackage.PackageId}", out GameSophonChunkBuild? build))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            build = await _client.GetGameSophonChunkBuildAsync(gameBranch, gameBranchPackage, gameBranchPackage.Tag, cancellationToken);
            _memoryCache.Set($"{nameof(GameSophonChunkBuild)}_{gameBranchPackage.PackageId}", build, TimeSpan.FromMinutes(1));
        }
        return build;
    }




    public async Task<GameSophonPatchBuild?> GetGameSophonPatchBuildAsync(GameBranch gameBranch, GameBranchPackage gameBranchPackage, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameSophonPatchBuild)}_{gameBranchPackage.PackageId}", out GameSophonPatchBuild? build))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            build = await _client.GetGameSophonPatchBuildAsync(gameBranch, gameBranchPackage, cancellationToken);
            _memoryCache.Set($"{nameof(GameSophonPatchBuild)}_{gameBranchPackage.PackageId}", build, TimeSpan.FromMinutes(1));
        }
        return build;
    }



    public async Task<List<GameDXConfig>> GetGameDXConfigsAsync(IEnumerable<GameId> gameIds, CancellationToken cancellationToken = default)
    {
        string key = $"{nameof(GameDXConfig)}_{string.Join(',', gameIds.Select(x => x.Id))}";
        if (!_memoryCache.TryGetValue(key, out List<GameDXConfig>? dxConfigs))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var launcherId = LauncherId.FromGameId(gameIds.First())!;
            var gpuInfos = GetGPUInfos();
            dxConfigs = await _client.GetDXConfigsAsync(launcherId, lang, gameIds, gpuInfos, cancellationToken);
            _memoryCache.Set(key, dxConfigs, TimeSpan.FromMinutes(5));
        }
        return dxConfigs!;
    }


    /// <summary>
    /// <b>平台替换</b>：上游用 SetupAPI（<c>SetupDiGetClassDevs(GUID_DEVCLASS_DISPLAY)</c>）枚举显卡并读
    /// <c>SPDRP_DEVICEDESC</c> / <c>DEVPKEY_Device_DriverVersion</c>。Linux 下改为读
    /// <c>/sys/class/drm/card*</c> 与 <c>/sys/module/&lt;driver&gt;/version</c>；这两项只用于向 HoYoPlay
    /// 请求 DX 配置，缺失时返回空列表（上游同样容忍异常）。见 docs/09-LINUX-DIVERGENCES.md。
    /// </summary>
    private static List<GPUInfo> GetGPUInfos()
    {
        var gpuInfos = new List<GPUInfo>();
        try
        {
            foreach (string card in Directory.EnumerateDirectories("/sys/class/drm", "card[0-9]*"))
            {
                string device = Path.Combine(card, "device");
                if (!Directory.Exists(device))
                {
                    continue;
                }
                string? name = ReadTrimmed(Path.Combine(device, "product_name"));
                string? driver = ReadDriver(Path.Combine(device, "uevent"));
                name ??= driver switch
                {
                    "amdgpu" or "radeon" => "AMD GPU",
                    "i915" or "xe" => "Intel GPU",
                    "nouveau" or "nvidia" or "nvidia-drm" => "NVIDIA GPU",
                    _ => null,
                };
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                string version = "";
                if (driver is not null && ReadTrimmed($"/sys/module/{driver}/version") is { } v)
                {
                    version = v;
                }
                gpuInfos.Add(new GPUInfo
                {
                    Name = name,
                    DriverVersion = version,
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        return gpuInfos;
    }


    private static string? ReadTrimmed(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }


    private static string? ReadDriver(string ueventPath)
    {
        try
        {
            if (!File.Exists(ueventPath))
            {
                return null;
            }
            foreach (string line in File.ReadLines(ueventPath))
            {
                if (line.StartsWith("DRIVER=", StringComparison.Ordinal))
                {
                    return line["DRIVER=".Length..].Trim();
                }
            }
        }
        catch (Exception)
        {
            // ignore
        }
        return null;
    }


}
