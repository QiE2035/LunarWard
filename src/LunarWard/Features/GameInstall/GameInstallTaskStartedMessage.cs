using LunarWard.RPC.GameInstall;

namespace LunarWard.Features.GameInstall;

/// <summary>
/// <b>移植自</b> <c>Starward/src/Starward/Features/GameInstall/GameInstallTaskStartedMessage.cs</c>。
/// 平台替换：上游 <c>Starward.RPC.GameInstall.GameInstallContext</c>（独立安装进程的 RPC 契约）
/// 在本仓库对应 <see cref="LunarWard.Installer.GameInstallContext"/>（同字段集合，进程内实现）。
/// </summary>
class GameInstallTaskStartedMessage
{
    public GameInstallContext InstallTask { get; init; }

    public GameInstallTaskStartedMessage(GameInstallContext installTask)
    {
        InstallTask = installTask;
    }
}
