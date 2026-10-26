using System.Text.Json.Serialization;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;

namespace LunarWard.Installer;

/// <summary>
/// 一次安装/更新/修复/预下载请求。
/// </summary>
/// <remarks>
/// 上游这是 gRPC 的 protobuf 消息（<c>GameInstall.proto</c>）。去掉 gRPC 后
/// 改为普通 POCO，字段与 proto 一一对应，便于 UI 直接绑定与序列化。
/// </remarks>
public sealed class GameInstallRequest
{
    /// <summary>例如 <c>hk4e_cn</c>。</summary>
    public string GameBiz { get; set; } = "";

    /// <summary>例如 <c>1Z8W5NHUQb</c>（原神国服的 product id）。</summary>
    public string GameId { get; set; } = "";

    public string InstallPath { get; set; } = "";

    public GameInstallOperation Operation { get; set; } = GameInstallOperation.Install;

    public AudioLanguage AudioLanguage { get; set; } = AudioLanguage.None;

    /// <summary>硬链接来源（同一份游戏给多个区服用）。</summary>
    public string? HardLinkPath { get; set; }


    public LunarWard.Core.HoYoPlay.GameId GetGameId()
        => new() { GameBiz = GameBiz, Id = GameId };


    public GameInstallContext ToTask() => new()
    {
        AudioLanguage = AudioLanguage,
        GameId = GetGameId(),
        HardLinkPath = HardLinkPath,
        InstallPath = InstallPath,
        Operation = Operation,
    };


    public static GameInstallRequest FromTask(GameInstallContext task) => new()
    {
        GameBiz = task.GameId.GameBiz,
        GameId = task.GameId.Id,
        InstallPath = task.InstallPath,
        Operation = task.Operation,
        AudioLanguage = task.AudioLanguage,
        HardLinkPath = task.HardLinkPath,
    };
}


/// <summary>
/// 安装任务的进度快照（可 JSON 序列化，供 UI/日志使用）。
/// </summary>
/// <remarks>字段与上游 <c>GameInstallContextDTO</c> 的 proto 字段一一对应。</remarks>
public sealed class GameInstallProgress
{
    public string GameBiz { get; set; } = "";

    public string GameId { get; set; } = "";

    public string InstallPath { get; set; } = "";

    public int Operation { get; set; }

    public int AudioLanguage { get; set; }

    public string? HardLinkPath { get; set; }

    public long Timestamp { get; set; }

    public int State { get; set; }

    public string? ErrorMessage { get; set; }

    public int DownloadMode { get; set; }

    public long ProgressDownloadTotalBytes { get; set; }

    public long ProgressDownloadFinishBytes { get; set; }

    public long ProgressReadTotalBytes { get; set; }

    public long ProgressReadFinishBytes { get; set; }

    public long ProgressWriteTotalBytes { get; set; }

    public long ProgressWriteFinishBytes { get; set; }

    public double ProgressPercent { get; set; }

    public long NetworkDownloadSpeed { get; set; }

    public long StorageReadSpeed { get; set; }

    public long StorageWriteSpeed { get; set; }

    public long RemainTimeSeconds { get; set; }


    [JsonIgnore]
    public GameIdSnapshot GameIdPair => new(GameBiz, GameId);


    public GameInstallContext UpdateTask(GameInstallContext? task = null)
    {
        task ??= new GameInstallContext
        {
            AudioLanguage = (AudioLanguage)AudioLanguage,
            GameId = new LunarWard.Core.HoYoPlay.GameId { GameBiz = GameBiz, Id = GameId },
            HardLinkPath = HardLinkPath,
            InstallPath = InstallPath,
        };
        task.Operation = (GameInstallOperation)Operation;
        task.Timestamp = Timestamp;
        task.State = (GameInstallState)State;
        task.Progress_DownloadTotalBytes = ProgressDownloadTotalBytes;
        task.Progress_DownloadFinishBytes = ProgressDownloadFinishBytes;
        task.Progress_ReadTotalBytes = ProgressReadTotalBytes;
        task.Progress_ReadFinishBytes = ProgressReadFinishBytes;
        task.Progress_WriteTotalBytes = ProgressWriteTotalBytes;
        task.Progress_WriteFinishBytes = ProgressWriteFinishBytes;
        task.Progress_Percent = ProgressPercent;
        task.ErrorMessage = ErrorMessage;
        task.NetworkDownloadSpeed = NetworkDownloadSpeed;
        task.StorageReadSpeed = StorageReadSpeed;
        task.StorageWriteSpeed = StorageWriteSpeed;
        task.RemainTimeSeconds = RemainTimeSeconds;
        task.DownloadMode = (GameInstallDownloadMode)DownloadMode;
        return task;
    }


    public static GameInstallProgress FromTask(GameInstallContext task) => new()
    {
        AudioLanguage = (int)task.AudioLanguage,
        GameBiz = task.GameId.GameBiz,
        GameId = task.GameId.Id,
        HardLinkPath = task.HardLinkPath,
        InstallPath = task.InstallPath,
        Operation = (int)task.Operation,
        Timestamp = task.Timestamp,
        State = (int)task.State,
        ProgressDownloadTotalBytes = task.Progress_DownloadTotalBytes,
        ProgressDownloadFinishBytes = task.Progress_DownloadFinishBytes,
        ProgressReadTotalBytes = task.Progress_ReadTotalBytes,
        ProgressReadFinishBytes = task.Progress_ReadFinishBytes,
        ProgressWriteTotalBytes = task.Progress_WriteTotalBytes,
        ProgressWriteFinishBytes = task.Progress_WriteFinishBytes,
        ProgressPercent = task.Progress_Percent,
        ErrorMessage = task.ErrorMessage,
        NetworkDownloadSpeed = task.NetworkDownloadSpeed,
        StorageReadSpeed = task.StorageReadSpeed,
        StorageWriteSpeed = task.StorageWriteSpeed,
        RemainTimeSeconds = task.RemainTimeSeconds,
        DownloadMode = (int)task.DownloadMode,
    };
}


/// <summary>只读的 GameId 快照（避免暴露可变引用）。</summary>
public readonly record struct GameIdSnapshot(string GameBiz, string Id);


/// <summary>限速请求/响应（字段名与 proto 一致）。</summary>
public sealed class RateLimiterMessage
{
    public int BytesPerSecond { get; set; }
}
