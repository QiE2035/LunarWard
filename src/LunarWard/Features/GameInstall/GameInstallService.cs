using Microsoft.Extensions.Logging;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.GameLauncher;
using LunarWard.Helpers;
using LunarWard.Installer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LunarWard.Features.GameInstall;

/// <summary>
/// 上游 <c>Starward.Features.GameInstall.GameInstallService</c> 的移植。
/// <para>
/// 平台替换（docs/09 §七）：上游是独立的 RPC 客户端——任务在另一个进程里跑，
/// 通过 gRPC 的 <c>GameInstallerClient</c>（<c>StartOrContinueTask</c> / <c>GetTaskProgress</c> /
/// <c>SyncGameInstallContext</c>）拿进度；Linux 上没有命名管道形态的 RPC 宿主，
/// 本仓库安装引擎是进程内的 <see cref="GameInstallController"/>（AppServices.Current.Installer），
/// 所以这里把 gRPC 调用换成控制器调用，进度改由 <see cref="GameInstallController.TaskStateChanged"/> 事件推送。
/// 方法名、返回类型、任务字典语义与上游保持一致。
/// </para>
/// </summary>
internal class GameInstallService
{

    private readonly ILogger<GameInstallService> _logger;

    private readonly GameInstallController _installer;

    private readonly GameLauncherService _gameLauncherService;


    public GameInstallService(ILogger<GameInstallService> logger, GameLauncherService gameLauncherService)
    {
        _logger = logger;
        _gameLauncherService = gameLauncherService;
        _installer = AppServices.Current.Installer;
        _installer.TaskStateChanged += (_, task) => AddOrUpdateTask(task);
    }




    private readonly ConcurrentDictionary<GameId, GameInstallContext> _tasks = new();





    public GameInstallContext? GetGameInstallTask(GameId gameId)
    {
        return _tasks.GetValueOrDefault(gameId);
    }





    /// <summary>
    /// 上游从 RPC 服务端同步任务列表；这里从进程内控制器同步当前任务。
    /// </summary>
    public Task SyncGameInstallTasksFromRPCAsync()
    {
        try
        {
            if (_installer.CurrentTask is GameInstallContext task)
            {
                AddOrUpdateTask(task);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync game install tasks");
        }
        return Task.CompletedTask;
    }





    /// <summary>
    /// 上游启动 gRPC 流式订阅进度；本地由 <see cref="GameInstallController.TaskStateChanged"/> 事件驱动，
    /// 保留空实现以对齐调用点。
    /// </summary>
    public void StartUpdateTaskProgress()
    {

    }




    private GameInstallContext AddOrUpdateTask(GameInstallContext task)
    {
        GameId gameId = task.GameId;
        _tasks[gameId] = task;
        if (task.State is GameInstallState.Stop or GameInstallState.Finish)
        {
            _tasks.TryRemove(gameId, out _);
        }
        return task;
    }




    public async Task<GameInstallContext?> StartInstallAsync(GameId gameId, string installPath, AudioLanguage audioLanguage)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.Install, gameId, installPath, audioLanguage);
    }



    public async Task<GameInstallContext?> StartPredownloadAsync(GameId gameId, string installPath, AudioLanguage audioLanguage)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.Predownload, gameId, installPath, audioLanguage);
    }



    public async Task<GameInstallContext?> StartUpdateAsync(GameId gameId, string installPath, AudioLanguage audioLanguage)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.Update, gameId, installPath, audioLanguage);
    }



    public async Task<GameInstallContext?> StartRepairAsync(GameId gameId, string installPath, AudioLanguage audioLanguage)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.Repair, gameId, installPath, audioLanguage);
    }



    private async Task<GameInstallContext?> StartOrContinueTaskAsync(GameInstallOperation operation, GameId gameId, string installPath, AudioLanguage audioLanguage)
    {
        try
        {
            var request = new GameInstallRequest
            {
                GameBiz = gameId.GameBiz,
                GameId = gameId.Id,
                InstallPath = installPath,
                Operation = operation,
                AudioLanguage = audioLanguage,
                HardLinkPath = await GetHardLinkPathAsync(gameId, installPath),
            };
            _logger.LogInformation("""
                Start game install task: 
                Operation: {operation}
                GameId: {gameId} {gameBiz}
                InstallPath: {installPath}
                AudioLanguage: {audioLanguage}
                HardLinkPath: {hardLinkPath}
                """, operation, gameId.Id, gameId.GameBiz, installPath, audioLanguage, request.HardLinkPath);
            _installer.StartOrContinue(request);
            if (_installer.CurrentTask is GameInstallContext task)
            {
                return AddOrUpdateTask(task);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start game install task ({GameBiz}, {Operation})", gameId.GameBiz, operation);
            return null;
        }
    }



    public async Task<GameInstallContext> PauseTaskAsync(GameInstallContext task)
    {
        try
        {
            GameInstallRequest request = GameInstallRequest.FromTask(task);
            _logger.LogInformation("Pause game install task: {gameId} {gameBiz}", task.GameId.Id, task.GameId.GameBiz);
            _installer.Pause(request);
            task.State = GameInstallState.Paused;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pause game install task ({GameBiz})", task.GameId.GameBiz);
        }
        return task;
    }



    public async Task<GameInstallContext> ContinueTaskAsync(GameInstallContext task)
    {
        try
        {
            GameInstallRequest request = GameInstallRequest.FromTask(task);
            _logger.LogInformation("Continue game install task: {gameId} {gameBiz}", task.GameId.Id, task.GameId.GameBiz);
            _installer.StartOrContinue(request);
            if (_installer.CurrentTask is GameInstallContext current)
            {
                return AddOrUpdateTask(current);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Continue game install task ({GameBiz})", task.GameId.GameBiz);
        }
        return task;
    }



    public async Task<GameInstallContext> StopTaskAsync(GameInstallContext task)
    {
        try
        {
            GameInstallRequest request = GameInstallRequest.FromTask(task);
            _logger.LogInformation("Stop game install task: {gameId} {gameBiz}", task.GameId.Id, task.GameId.GameBiz);
            _installer.Stop(request);
            task.State = GameInstallState.Stop;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stop game install task ({GameBiz})", task.GameId.GameBiz);
        }
        return task;
    }



    /// <summary>
    /// 卸载游戏。
    /// <para>平台替换：上游通过 RPC 调 <c>UninstallGameAsync</c>（按官方 uninstall 清单删文件、可选保留截图/用户数据）。
    /// 本地安装引擎没有卸载流程，这里按同样的安全边界直接删目录：安装目录必须存在、不能是盘符根、必须是绝对路径。</para>
    /// </summary>
    public async Task<bool> StartUninstallAsync(GameId gameId, string installPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installPath) || !Path.IsPathRooted(installPath))
            {
                return false;
            }
            if (!Directory.Exists(installPath))
            {
                _logger.LogWarning("Game folder does not exist: {installPath}", installPath);
                return false;
            }
            if (Path.GetPathRoot(installPath)?.TrimEnd(Path.DirectorySeparatorChar) == installPath.TrimEnd(Path.DirectorySeparatorChar))
            {
                throw new InvalidOperationException("Game folder is the root of drive.");
            }
            _logger.LogInformation("""
                Start uninstall game:
                GameId: {gameId} {gameBiz}
                InstallPath: {installPath}
                """, gameId.Id, gameId.GameBiz, installPath);
            await Task.Run(() => Directory.Delete(installPath, true));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall game failed ({GameBiz})", gameId.GameBiz);
            throw;
        }
    }



    private async Task<string?> GetHardLinkPathAsync(GameId gameId, string installPath)
    {
        if (!AppConfig.EnableHardLink)
        {
            return null;
        }
        if (GameFeatureConfig.FromGameId(gameId).SupportHardLink)
        {
            string game = gameId.GameBiz.Game;
            Version? lastVersion = null;
            string? lastPath = null;
            foreach (string server in new[] { "cn", "bilibili", "global", })
            {
                string biz = $"{game}_{server}";
                if (gameId.GameBiz != biz)
                {
                    if (_tasks.Values.FirstOrDefault(x => x.GameId.GameBiz == biz) is GameInstallContext task)
                    {
                        if (task.Operation is GameInstallOperation.Install or GameInstallOperation.Update or GameInstallOperation.Repair)
                        {
                            if (!string.IsNullOrWhiteSpace(task.InstallPath) && Path.GetPathRoot(task.InstallPath) == Path.GetPathRoot(installPath) && DriveHelper.GetDriveFormat(installPath) is "NTFS")
                            {
                                return task.InstallPath;
                            }
                        }
                    }
                    string? path = GameLauncherService.GetGameInstallPath(biz);
                    if (!string.IsNullOrWhiteSpace(path) && Path.GetPathRoot(path) == Path.GetPathRoot(installPath) && DriveHelper.GetDriveFormat(path) is "NTFS")
                    {
                        Version? version = await _gameLauncherService.GetLocalGameVersionAsync(biz, path);
                        if (lastPath is null)
                        {
                            lastVersion = version;
                            lastPath = path;
                        }
                        else if (version > lastVersion)
                        {
                            lastVersion = version;
                            lastPath = path;
                        }
                    }

                }
            }
            return lastPath;
        }
        return null;
    }



}
