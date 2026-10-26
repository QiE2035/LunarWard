using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.GameSetting;
using LunarWard.Features.HoYoPlay;
using LunarWard.Helpers;
using LunarWard.Platform;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 启动游戏。<b>移植自</b> <c>Starward/src/Starward/Features/GameLauncher/GameLauncherService.cs</c>。
/// </summary>
/// <remarks>
/// 平台替换点（全部登记在 <c>docs/09-LINUX-DIVERGENCES.md</c>）：
/// <list type="bullet">
///   <item>进程探测：Windows <c>Process.GetProcessesByName</c> + SessionId + 线程挂起判断
///   → Linux 按流程名匹配 + <c>/proc/&lt;pid&gt;/stat</c> 状态位 <c>T</c> 判挂起。</item>
///   <item>启动：Windows <c>UseShellExecute + Verb=runas</c> → Linux 用 <c>wine</c> 拉 <c>.exe</c>，
///   <c>WINEPREFIX</c> 取 <see cref="GamePrefixContext.CurrentPrefix"/>；<c>cmd /c start</c>
///   无等价物（<c>AppConfig.StartGameWithCMD</c> 在 Linux 被忽略）。</item>
///   <item>时长统计：上游 <c>PlayTimeRecordService.StartProcessToLogAsync</c>；本仓库由
///   <c>Launch.PlayTimeService</c>（外壳调用）负责，这里不重复记录。</item>
/// </list>
/// </remarks>
internal partial class GameLauncherService
{


    private readonly ILogger<GameLauncherService> _logger;


    private readonly HoYoPlayService _hoYoPlayService;

    private readonly GameAuthLoginService _gameAuthLoginService;


    public GameLauncherService(ILogger<GameLauncherService> logger, HoYoPlayService hoYoPlayService, GameAuthLoginService gameAuthLoginService)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
        _gameAuthLoginService = gameAuthLoginService;
    }





    /// <summary>
    /// 游戏安装目录，为空时未找到
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetGameInstallPath(GameId gameId)
    {
        return GetGameInstallPath(gameId.GameBiz);
    }


    /// <summary>
    /// 游戏安装目录，为空时未找到
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetGameInstallPath(GameBiz gameBiz)
    {
        var path = AppConfig.GetGameInstallPath(gameBiz);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        path = GetFullPathIfRelativePath(path);
        if (Directory.Exists(path))
        {
            return Path.GetFullPath(path);
        }
        else if (AppConfig.GetGameInstallPathRemovable(gameBiz))
        {
            return path;
        }
        else
        {
            ChangeGameInstallPath(gameBiz, null);
            return null;
        }
    }



    /// <summary>
    /// 游戏安装目录，为空时未找到
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="storageRemoved">可移动存储设备已移除</param>
    /// <returns></returns>
    public static string? GetGameInstallPath(GameId gameId, out bool storageRemoved)
    {
        storageRemoved = false;
        var path = AppConfig.GetGameInstallPath(gameId.GameBiz);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        path = GetFullPathIfRelativePath(path);
        if (Directory.Exists(path))
        {
            return path;
        }
        else if (AppConfig.GetGameInstallPathRemovable(gameId.GameBiz))
        {
            storageRemoved = true;
            return path;
        }
        else
        {
            ChangeGameInstallPath(gameId, null);
            return null;
        }
    }



    /// <summary>
    /// 本地游戏版本
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="installPath"></param>
    /// <returns></returns>
    public async Task<Version?> GetLocalGameVersionAsync(GameId gameId, string? installPath = null)
    {
        return await GetLocalGameVersionAsync(gameId.GameBiz, installPath);
    }



    /// <summary>
    /// 本地游戏版本
    /// </summary>
    /// <param name="gameBiz"></param>
    /// <param name="installPath"></param>
    /// <returns></returns>
    public async Task<Version?> GetLocalGameVersionAsync(GameBiz gameBiz, string? installPath = null)
    {
        installPath ??= GetGameInstallPath(gameBiz);
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }
        var config = Path.Join(installPath, "config.ini");
        if (File.Exists(config))
        {
            var str = await File.ReadAllTextAsync(config);
            var matches = GameVersionRegex().Matches(str);
            Version? version = null;
            if (matches.Count > 0)
            {
                _ = Version.TryParse(matches[^1].Groups[1].Value, out version);
            }
            return version;
        }
        else
        {
            _logger.LogWarning("config.ini not found: {path}", config);
            return null;
        }
    }


    [GeneratedRegex(@"game_version=(.+)")]
    private static partial Regex GameVersionRegex();



    /// <summary>
    /// 最新游戏版本
    /// </summary>
    /// <param name="gameBiz"></param>
    /// <returns></returns>
    public async Task<(Version? Latest, Version? Predownload)> GetLatestGameVersionAsync(GameId gameId)
    {
        GameConfig? config = await _hoYoPlayService.GetGameConfigAsync(gameId);
        if (config is null)
        {
            throw new ArgumentOutOfRangeException($"Game config is null ({gameId.Id}, {gameId.GameBiz}).");
        }
        if (config.DefaultDownloadMode is DownloadMode.DOWNLOAD_MODE_CHUNK or DownloadMode.DOWNLOAD_MODE_LDIFF)
        {
            GameBranch? gameBranch = await _hoYoPlayService.GetGameBranchAsync(gameId);
            if (gameBranch is null)
            {
                throw new ArgumentOutOfRangeException($"Game branch is null ({gameId.Id}, {gameId.GameBiz}).");
            }
            _ = Version.TryParse(gameBranch.Main.Tag, out Version? latestVersion);
            _ = Version.TryParse(gameBranch.PreDownload?.Tag, out Version? predownloadVersion);
            return (latestVersion, predownloadVersion);
        }
        else
        {
            GamePackage package = await _hoYoPlayService.GetGamePackageAsync(gameId);
            _ = Version.TryParse(package.Main.Major?.Version, out Version? latestVersion);
            _ = Version.TryParse(package.PreDownload.Major?.Version, out Version? predownloadVersion);
            return (latestVersion, predownloadVersion);
        }
    }




    /// <summary>
    /// 游戏进程名，带 .exe 扩展名
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public async Task<string> GetGameExeNameAsync(GameId gameId)
    {
        string? name = GetGameExeName(gameId.GameBiz);
        if (string.IsNullOrWhiteSpace(name))
        {
            var config = await _hoYoPlayService.GetGameConfigAsync(gameId);
            name = config?.ExeFileName;
        }
        return name ?? throw new ArgumentOutOfRangeException($"Unknown game ({gameId.Id}, {gameId.GameBiz}).");
    }



    /// <summary>
    /// 游戏进程名，带 .exe 扩展名
    /// </summary>
    /// <param name="gameBiz"></param>
    /// <returns></returns>
    public static string? GetGameExeName(GameBiz gameBiz)
    {
        string? name = gameBiz.Value switch
        {
            GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => "YuanShen.exe",
            GameBiz.hk4e_global => "GenshinImpact.exe",
            _ => gameBiz.Game switch
            {
                GameBiz.hkrpg => "StarRail.exe",
                GameBiz.bh3 => "BH3.exe",
                GameBiz.nap => "ZenlessZoneZero.exe",
                _ => null,
            },
        };
        return name;
    }



    /// <summary>
    /// 游戏进程文件是否存在
    /// </summary>
    /// <param name="biz"></param>
    /// <param name="installPath"></param>
    /// <returns></returns>
    public async Task<bool> IsGameExeExistsAsync(GameId gameId, string? installPath = null)
    {
        installPath ??= GetGameInstallPath(gameId);
        if (!string.IsNullOrWhiteSpace(installPath))
        {
            var exe = Path.Join(installPath, await GetGameExeNameAsync(gameId));
            return File.Exists(exe);
        }
        return false;
    }



    /// <summary>
    /// 获取游戏进程
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public async Task<Process?> GetGameProcessAsync(GameId gameId)
    {
        // 平台替换：Windows 用 Process.GetProcessesByName + SessionId 过滤；
        // Linux 下 Wine 进程的 ProcessName 就是 "YuanShen.exe"，直接用同名匹配即可
        // （没有 Windows 的 Session 概念，DXVK 之外也不需要过滤）。
        var name = (await GetGameExeNameAsync(gameId)).Replace(".exe", "");
        return Process.GetProcesses()
                      .Where(x => string.Equals(x.ProcessName, name, StringComparison.OrdinalIgnoreCase))
                      .Where(x => !IsProcessPending(x))
                      .FirstOrDefault();
    }



    /// <summary>
    /// 检测进程挂起
    /// </summary>
    /// <param name="process"></param>
    /// <returns></returns>
    public static bool IsProcessPending(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }
            // 平台替换：Windows 遍历 ProcessThread.ThreadState/WaitReason；
            // Linux 读 /proc/<pid>/stat 的第 3 个字段，'T' = stopped（等价于挂起）。
            string stat = File.ReadAllText($"/proc/{process.Id}/stat");
            int close = stat.LastIndexOf(')');
            if (close < 0 || close + 2 >= stat.Length)
            {
                return false;
            }
            return stat[close + 2] is 'T';
        }
        catch { }
        return true;
    }



    /// <summary>
    /// 启动游戏
    /// </summary>
    /// <returns></returns>
    public async Task<Process?> StartGameAsync(GameId gameId, string? installPath = null)
    {
        try
        {
            if (await GetGameProcessAsync(gameId) is Process existingProcess)
            {
                throw new Exception($"Game is running: {existingProcess.ProcessName}.exe ({existingProcess.Id}).");
            }
            string? exe = null, arg = null;
            if (Directory.Exists(installPath))
            {
                var e = Path.Join(installPath, await GetGameExeNameAsync(gameId));
                if (File.Exists(e))
                {
                    exe = e;
                }
            }
            if (string.IsNullOrWhiteSpace(exe) && AppConfig.GetEnableThirdPartyTool(gameId.GameBiz))
            {
                exe = GetThirdPartyToolPath(gameId);
                if (!File.Exists(exe))
                {
                    SetThirdPartyToolPath(gameId, null);
                    _logger.LogWarning("Third party tool not found: {path}", exe);
                    exe = null;
                }
            }
            if (string.IsNullOrWhiteSpace(exe))
            {
                var folder = GetGameInstallPath(gameId);
                var name = await GetGameExeNameAsync(gameId);
                exe = Path.Join(folder, name);
                if (!File.Exists(exe))
                {
                    _logger.LogWarning("Game exe not found: {path}", exe);
                    throw new FileNotFoundException("Game exe not found", name);
                }
            }
            arg = AppConfig.GetStartArgument(gameId.GameBiz)?.Trim();
            if (AppConfig.EnableLoginAuthTicket is true)
            {
                string? ticket = await _gameAuthLoginService.CreateAuthTicketByGameBiz(gameId);
                if (!string.IsNullOrWhiteSpace(ticket))
                {
                    arg += $" login_auth_ticket={ticket}";
                }
            }
            if (AppConfig.GetUsePopupWindow(gameId.GameBiz))
            {
                arg += " -popupwindow";
            }
            if (AppConfig.GetEnableDX12(gameId.GameBiz))
            {
                arg += " -use-d3d12";
            }

            if (gameId.GameBiz.Game is GameBiz.hk4e)
            {
                await GameSettingService.SetGenshinEnableHDRAsync(gameId.GameBiz, AppConfig.EnableGenshinHDR);
            }
            _logger.LogInformation("Start game ({biz})\r\npath: {exe}\r\narg: {arg}", gameId, exe, arg);
            var info = BuildStartInfo(gameId, exe, arg);
            Process? process = Process.Start(info);
            return process;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start game ({biz}) failed", gameId);
        }
        return null;
    }




    /// <summary>
    /// 平台替换：Windows <c>UseShellExecute + Verb=runas</c>；Linux 用 wine 起 <c>.exe</c>/<c>.bat</c>，
    /// 原生可执行（<c>.sh</c> 等）直接起。<c>AppConfig.StartGameWithCMD</c>（<c>cmd /c start</c>）无等价物，
    /// 这里忽略。
    /// </summary>
    /// <remarks>
    /// 环境注入与 <see cref="LunarWard.Launch.LaunchPlanFactory"/> 的 Baseline 层保持一致
    /// （<c>WINEPREFIX</c>/<c>WINEARCH</c>/<c>WINEDLLOVERRIDES</c>），并补上游在 Windows 上由系统承担、
    /// 在 Linux 上必须由 DXVK/VKD3D 承担的 DX12 变量（仅 ZZZ，见 <see cref="LunarWard.Games.GameAdapter.BuildEnv"/>）。
    /// </remarks>
    /// <remarks>
    /// 声明为 <c>public</c> 是本移植的可测缝：单测直接断言 wine 路径、命令行字符串与环境注入，
    /// 不启动真实进程（见 docs/14）。
    /// </remarks>
    public static ProcessStartInfo BuildStartInfo(GameId gameId, string exe, string? arg, bool? enableDX12 = null)
    {
        string workDir = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory;
        string extension = Path.GetExtension(exe);

        if (extension is ".exe" or ".bat")
        {
            // 上游把整条命令行交给 CreateProcess（Windows 自己解析引号）；wine 同样接受单条命令行，
            // 因此这里保持「单字符串」语义，不能按空格拆成 ArgumentList（会破坏带空格的参数）。
            var info = new ProcessStartInfo
            {
                FileName = GamePrefixContext.CurrentWineBinary ?? "wine",
                Arguments = arg is null ? $"\"{exe}\"" : $"\"{exe}\" {arg}",
                UseShellExecute = false,
                WorkingDirectory = workDir,
            };

            if (GamePrefixContext.CurrentPrefix is { } prefix)
            {
                info.Environment["WINEPREFIX"] = prefix;
            }
            info.Environment["WINEARCH"] = "win64";
            info.Environment["WINEDLLOVERRIDES"] = "winemenubuilder.exe=d;mscoree,mshtml=";

            // 平台替换：上游在 Windows 上用原生 DX12；Linux 走 DXVK/VKD3D，需要显式开启 nvapi 与 DXR。
            try
            {
                // GameBiz.Game 用上游的 hk4e/nap 口径，GameAdapter 用 GameCatalog 的 genshin/zzz 口径。
                string gameKind = gameId.GameBiz.Game switch
                {
                    "hk4e" => LunarWard.Games.GameCatalog.Genshin,
                    "nap" => LunarWard.Games.GameCatalog.Zzz,
                    var other => other,
                };
                var adapter = LunarWard.Games.GameAdapter.BuildEnv(
                    gameKind: gameKind,
                    wineWayland: false,
                    timeoutFix: false,
                    borderless: false,
                    dx12: enableDX12 ?? AppConfig.GetEnableDX12(gameId.GameBiz),
                    fsr: false);
                foreach ((string key, string value) in adapter.Env)
                {
                    info.Environment[key] = value;
                }
            }
            catch (Exception)
            {
                // 适配层失败不能挡住启动本身。
            }

            return info;
        }

        return new ProcessStartInfo
        {
            FileName = exe,
            Arguments = arg ?? "",
            UseShellExecute = false,
            WorkingDirectory = workDir,
        };
    }


    /// <summary>
    /// 修改游戏安装目录
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string? ChangeGameInstallPath(GameId gameId, string? path)
    {
        return ChangeGameInstallPath(gameId.GameBiz, path);
    }


    /// <summary>
    /// 修改游戏安装目录
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string? ChangeGameInstallPath(GameBiz gameBiz, string? path)
    {
        if (Directory.Exists(path))
        {
            path = Path.GetFullPath(path);
            string relativePath = GetRelativePathIfInRemovableStorage(path, out bool removable);
            AppConfig.SetGameInstallPath(gameBiz, relativePath);
            AppConfig.SetGameInstallPathRemovable(gameBiz, removable);
        }
        else
        {
            path = null;
            AppConfig.SetGameInstallPath(gameBiz, null);
            AppConfig.SetGameInstallPathRemovable(gameBiz, false);
        }
        return path;
    }



    /// <summary>
    /// 如果安装在可移动存储设备中，获取相对路径
    /// </summary>
    /// <param name="path"></param>
    /// <param name="removableStorage"></param>
    /// <returns></returns>
    public static string GetRelativePathIfInRemovableStorage(string path, out bool removableStorage)
    {
        removableStorage = DriveHelper.IsDeviceRemovableOrOnUSB(path);
        if (removableStorage && Path.GetPathRoot(AppConfig.StarwardExecutePath) == Path.GetPathRoot(path))
        {
            path = Path.GetRelativePath(Path.GetDirectoryName(AppConfig.ConfigPath)!, path);
        }
        return path;
    }



    /// <summary>
    /// 如果安装在可移动存储设备中，获取完整路径
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string GetFullPathIfRelativePath(string path)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }
        else
        {
            return Path.GetFullPath(path, Path.GetDirectoryName(AppConfig.ConfigPath)!);
        }
    }




    /// <summary>
    /// 获取第三方工具路径
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetThirdPartyToolPath(GameId gameId)
    {
        string? path = AppConfig.GetThirdPartyToolPath(gameId.GameBiz);
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = GetFullPathIfRelativePath(path);
        }
        if (File.Exists(path))
        {
            return path;
        }
        else
        {
            AppConfig.SetThirdPartyToolPath(gameId.GameBiz, null);
            return null;
        }
    }


    /// <summary>
    /// 设置第三方工具路径
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string? SetThirdPartyToolPath(GameId gameId, string? path)
    {
        if (File.Exists(path))
        {
            path = Path.GetFullPath(path);
            string relativePath = GetRelativePathIfInRemovableStorage(path, out bool removable);
            AppConfig.SetThirdPartyToolPath(gameId.GameBiz, relativePath);
        }
        else
        {
            path = null;
            AppConfig.SetThirdPartyToolPath(gameId.GameBiz, null);
        }
        return path;
    }




}
