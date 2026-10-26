using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.HoYoPlay;
using Microsoft.Extensions.Logging;

namespace LunarWard.Features.Background;

/// <summary>
/// 背景图/视频的缓存、下载与选取（对应上游
/// <c>Starward/Features/Background/BackgroundService.cs</c>，306 行）。
/// </summary>
/// <remarks>
/// <para>与上游一致：背景文件缓存于 <c>AppConfig.CacheFolder/bg</c>，API 背景 + 游戏海报 + 自定义背景
/// 合并为 <see cref="GetGameBackgroundsAsync"/>，上次选择记录在 <c>AppConfig.GetBg/GetGameBackgroundIds</c>。</para>
/// <para>平台替换：<c>XamlRoot</c> + <c>FileOpenPicker</c> → <c>TopLevel.StorageProvider</c>；
/// <c>StorageFile</c> → <c>IStorageFile</c>；<c>BitmapDecoder</c> 校验 → Avalonia <see cref="Bitmap"/> 解码；
/// 视频「能否解码」不再预先验证（Linux 走 ffmpeg 子进程），播放失败时上层回退，登记于 docs/09。</para>
/// </remarks>
public class BackgroundService
{

    private readonly ILogger<BackgroundService> _logger;

    private readonly HoYoPlayService _hoYoPlayService;

    private readonly HttpClient _httpClient;



    public BackgroundService(ILogger<BackgroundService> logger, HoYoPlayService hoYoPlayService, HttpClient httpClient)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
        _httpClient = httpClient;
    }



    /// <summary>
    /// 获取背景图文件路径，保存在 CacheFolder\bg
    /// </summary>
    [return: NotNullIfNotNull(nameof(name))]
    public static string? GetBgFilePath(string? name)
    {
        return Path.Join(AppConfig.CacheFolder, "bg", name);
    }


    /// <summary>
    /// 获取自定义背景图文件路径
    /// </summary>
    public static bool TryGetCustomBgFilePath(GameId gameId, [NotNullWhen(true)] out string? path)
    {
        path = null;
        if (gameId is null)
        {
            return false;
        }
        if (AppConfig.GetEnableCustomBg(gameId.GameBiz))
        {
            path = GetBgFilePath(AppConfig.GetCustomBg(gameId.GameBiz));
            if (File.Exists(path))
            {
                return true;
            }
        }
        return false;
    }


    /// <summary>
    /// 文件是否是支持的视频格式，支持 mp4、mkv、webm
    /// </summary>
    public static bool FileIsSupportedVideo(string? name)
    {
        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".mp4" or ".mkv" or ".webm" => true,
            _ => false,
        };
    }


    /// <summary>
    /// 获取已缓存的背景图文件路径
    /// </summary>
    public static string? GetCachedBackgroundFile(GameId gameId)
    {
        if (gameId is null)
        {
            return null;
        }
        if (TryGetCustomBgFilePath(gameId, out string? path))
        {
            return path;
        }
        path = GetBgFilePath(AppConfig.GetBg(gameId.GameBiz));
        return File.Exists(path) ? path : null;
    }



    /// <summary>
    /// 背景图和版本海报链接
    /// </summary>
    public async Task<List<GameBackground>> GetGameBackgroundsAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        GameBackgroundInfo backgroundInfo = await _hoYoPlayService.GetGameBackgroundAsync(gameId, cancellationToken);
        List<GameBackground> backgrounds = backgroundInfo?.Backgrounds?.ToList() ?? [];
        GameInfo gameInfo = await _hoYoPlayService.GetGameInfoAsync(gameId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(gameInfo?.Display?.Background?.Url))
        {
            backgrounds.Add(GameBackground.FromPosterUrl(gameInfo.Display.Background.Url));
        }
        if (TryGetCustomBgFilePath(gameId, out string? path))
        {
            backgrounds.Add(GameBackground.FromCustomFile(path));
        }
        return backgrounds;
    }



    public async Task<GameBackground?> GetSuggestedGameBackgroundAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (TryGetCustomBgFilePath(gameId, out string? file))
        {
            return GameBackground.FromCustomFile(file);
        }
        List<GameBackground> backgrounds = await GetGameBackgroundsAsync(gameId, cancellationToken);
        GameBackground? bg = null;
        string? lastBg = AppConfig.GetBg(gameId.GameBiz);
        string? lastBgIds = AppConfig.GetGameBackgroundIds(gameId.GameBiz);
        string firstBgId = backgrounds.FirstOrDefault()?.Id ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(lastBg) && (lastBgIds?.StartsWith(firstBgId) ?? false))
        {
            // 没有新背景
            if (backgrounds.FirstOrDefault(x => Path.GetFileName(x.Background.Url) == lastBg) is GameBackground bg1)
            {
                bg1.StopVideo = true;
                bg = bg1;
            }
            else if (backgrounds.Where(x => x.Video != null).FirstOrDefault(x => Path.GetFileName(x.Video.Url) == lastBg) is GameBackground bg2)
            {
                bg = bg2;
            }
        }
        bg ??= backgrounds.FirstOrDefault();
        return bg;
    }



    public async Task<string> GetBackgroundFileAsync(string url, CancellationToken cancellationToken = default)
    {
        string name = Path.GetFileName(url);
        string file = GetBgFilePath(name);
        if (!File.Exists(file))
        {
            var bytes = await _httpClient.GetByteArrayAsync(url, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, bytes, cancellationToken);
        }
        return file;
    }




    /// <summary>
    /// 获取默认的背景图文件路径
    /// </summary>
    public static string? GetFallbackBackgroundImage(GameId gameId)
    {
        string? bg = GetBgFilePath(AppConfig.GetBg(gameId.GameBiz));
        if (!File.Exists(bg))
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Image", "UI_CutScene_1130320101A.png");
            bg = File.Exists(path) ? path : null;
        }
        return bg;
    }



    /// <summary>
    /// 更改自定义背景图文件，保存在 UserData\bg，返回文件名
    /// </summary>
    public async Task<string?> ChangeCustomBackgroundFileAsync(TopLevel topLevel)
    {
        string? file = await PickBackgroundFileAsync(topLevel);
        if (file is null)
        {
            return null;
        }
        await CheckBackgroundFileAvailableAsync(file);
        string bg = Path.Join(AppConfig.CacheFolder, "bg");
        Directory.CreateDirectory(bg);
        string name = Path.GetFileName(file);
        string path = Path.Combine(bg, name);
        if (path != file)
        {
            File.Copy(file, path, true);
        }
        return name;
    }



    /// <summary>
    /// 更改自定义背景图文件，保存在 UserData\bg，返回文件名
    /// </summary>
    public static async Task<string?> ChangeCustomBackgroundFileAsync(IStorageFile file)
    {
        string bg = Path.Join(AppConfig.CacheFolder, "bg");
        string? filePath = file.TryGetLocalPath();
        if (filePath is null)
        {
            return null;
        }
        if (Path.GetDirectoryName(filePath) != bg)
        {
            await CheckBackgroundFileAvailableAsync(filePath);
            Directory.CreateDirectory(bg);
            string path = Path.Combine(bg, file.Name);
            await using var dest = File.Create(path);
            await using var stream = await file.OpenReadAsync();
            await stream.CopyToAsync(dest);
        }
        return file.Name;
    }


    /// <summary>
    /// 选择背景图文件
    /// </summary>
    private static async Task<string?> PickBackgroundFileAsync(TopLevel topLevel)
    {
        var filter = new FilePickerFileType[]
        {
            new ("Image") { Patterns = [ "*.bmp", "*.jpg", "*.png", "*.webp", "*.avif", "*.jxl" ] },
            new ("Video") { Patterns = [ "*.mp4", "*.mkv", "*.webm" ] },
        };
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = filter,
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }



    /// <summary>
    /// 检查背景图文件是否可用
    /// </summary>
    private static async Task CheckBackgroundFileAvailableAsync(string file)
    {
        if (FileIsSupportedVideo(file))
        {
            if (!File.Exists(file) || new FileInfo(file).Length == 0)
            {
                throw new IOException($"Background video is not available: {file}");
            }
            await Task.CompletedTask;
        }
        else
        {
            using var fs = File.OpenRead(file);
            using var bitmap = new Bitmap(fs);
            await Task.CompletedTask;
        }
    }


}
