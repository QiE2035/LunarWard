using System;
using System.Threading;
using System.Threading.Tasks;
using LunarWard.Core.HoYoPlay;

namespace LunarWard.Installer;

/// <summary>
/// 安装器对 UI 的公开门面（上游是 Starward 的 RPC <c>GameInstall</c> 服务；
/// 本项目里安装器以类库形式内嵌，UI 直接用这个类，不再经过 RPC）。
///
/// <para>职责：把 <c>internal</c> 的 <see cref="GameInstallService"/> /
/// <see cref="GamePackageService"/> / <see cref="GameInstallHelper"/> 组装起来，
/// 暴露"开始安装/更新/预下载、暂停、继续、停止、读进度"这几件事。</para>
///
/// <para>线程模型与上游一致：<see cref="StartOrContinue"/> 只是把任务放进队列并返回
/// 当前进度快照，真正的下载/解压/校验在 <see cref="RunAsync"/> 里跑（调用方 fire-and-forget），
/// 进程退出时调用 <see cref="StopAllTasks"/>。</para>
/// </summary>
public sealed class GameInstallController : IDisposable
{
    private readonly GameInstallService _service;
    private readonly GamePackageService _packages;
    private readonly IInstallerLogger _logger;
    private bool _disposed;


    public GameInstallController(IInstallerLogger? logger = null)
    {
        _logger = logger ?? NullInstallerLogger.Instance;
        var httpClient = InstallerHttpClientFactory.Instance.CreateClient();
        _packages = new GamePackageService(_logger, new HoYoPlayClient(), httpClient);
        _service = new GameInstallService(_logger, () => _packages, new GameInstallHelper(_logger));
        _service.TaskStateChanged += (_, context) => { /* 交给 Progress 通道，见下 */ };
    }


    /// <summary>下载/解压进度回调（每帧一次快照）。</summary>
    public IProgress<GameInstallProgress>? Progress
    {
        get => _service.Progress;
        set => _service.Progress = value;
    }


    /// <summary>任务状态变化（开始/暂停/恢复/完成/出错）。</summary>
    public event EventHandler<GameInstallContext>? TaskStateChanged
    {
        add => _service.TaskStateChanged += value;
        remove => _service.TaskStateChanged -= value;
    }


    /// <summary>当前正在跑的任务（没有则为 null）。</summary>
    public GameInstallContext? CurrentTask => _service.CurrentTask;


    /// <summary>本机已安装游戏的版本（读 config.ini，读不到返回 null）。</summary>
    public Task<Version?> GetLocalGameVersionAsync(string installPath) => _packages.GetLocalGameVersionAsync(installPath);


    /// <summary>
    /// 取游戏安装包的元数据（版本、各语言音频包、压缩/解压大小）。
    /// <paramref name="launcherId"/> 是启动器 id（如 <c>1Z8W5NHUQb</c>）。
    /// </summary>
    public async Task<GamePackage?> GetGamePackageAsync(
        string launcherId,
        string language,
        GameId gameId,
        CancellationToken cancellationToken = default)
    {
        var client = new HoYoPlayClient();
        return await client.GetGamePackageAsync(launcherId, language, gameId, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>开始（或继续）一个安装任务，返回当前进度快照。</summary>
    public GameInstallProgress StartOrContinue(GameInstallRequest request) => _service.StartOrContinueTask(request);


    /// <summary>暂停任务。</summary>
    public GameInstallProgress Pause(GameInstallRequest request) => _service.PauseTask(request);


    /// <summary>停止任务（会从任务表里移除）。</summary>
    public GameInstallProgress Stop(GameInstallRequest request) => _service.StopTask(request);


    /// <summary>任务体：准备清单 → 下载 → 解压 → 校验 → 安装/更新。调用方不要 await 在主线程。</summary>
    public async Task RunAsync(GameInstallContext context, CancellationToken cancellationToken = default)
    {
        await _service.PrepareGameInstallTaskAsync(context, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>宿主退出时停止所有任务。</summary>
    public void StopAllTasks(string? reason = null) => _service.StopAllTasks(reason);


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service.Dispose();
    }
}
