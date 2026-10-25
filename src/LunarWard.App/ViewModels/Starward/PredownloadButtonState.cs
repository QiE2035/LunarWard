using LunarWard.Installer;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 预下载按钮的纯逻辑（Starward <c>Features/GameInstall/PreDownloadButton.xaml.cs</c>）：
/// 图标、按钮文案、弹层状态行、字节/速度/剩余时间格式化。
/// </summary>
/// <remarks>
/// 抽成静态类是为了让文案映射和数值阈值能被单测覆盖——VM 里只负责把安装器快照塞进来。
/// </remarks>
public static class PredownloadButtonState
{

    /// <summary>Starward <c>GetButtonIcon</c>：下载 <c>EBD3</c> / 悬停待续播 <c>F5B0</c> / 悬停暂停 <c>E62E</c>。</summary>
    public static string Icon(GameInstallState state, bool pointerOver)
    {
        if (pointerOver && state is GameInstallState.Paused or GameInstallState.Error or GameInstallState.Queueing)
        {
            return "\uF5B0";
        }
        if (pointerOver && state is GameInstallState.Downloading)
        {
            return "\uE62E";
        }
        return "\uEBD3";
    }


    /// <summary>Starward <c>GetButtonText</c>：预下载 / 等待中 / 百分比 / 已暂停 / 已完成 / 出错了。</summary>
    public static string Text(GameInstallState state, bool finished, string percentText, Func<string, string> localize)
    {
        if (finished || state is GameInstallState.Finish)
        {
            return localize("Launcher.PreInstallFinished");
        }
        return state switch
        {
            GameInstallState.Waiting or GameInstallState.Queueing => localize("StartGameButton.Waiting"),
            GameInstallState.Downloading => percentText,
            GameInstallState.Paused => localize("DownloadGamePage.Paused"),
            GameInstallState.Error => localize("DownloadGamePage.SomethingError"),
            _ => localize("Launcher.PreInstall"),
        };
    }


    /// <summary>弹层第一行（Starward <c>GetInstallStateText</c>）。</summary>
    public static string InstallStateText(GameInstallState state, bool finished, Func<string, string> localize)
    {
        if (finished || state is GameInstallState.Finish)
        {
            return localize("Launcher.PreInstallFinished");
        }
        return state switch
        {
            GameInstallState.Waiting or GameInstallState.Queueing => localize("StartGameButton.Waiting"),
            GameInstallState.Downloading => localize("DownloadGamePage.Downloading"),
            GameInstallState.Paused => localize("DownloadGamePage.Paused"),
            GameInstallState.Error => localize("DownloadGamePage.SomethingError"),
            _ => localize("Launcher.PreInstall"),
        };
    }


    /// <summary>Starward <c>IsButtonEnabled</c>：完成后按钮消失/不可点。</summary>
    public static bool ButtonEnabled(GameInstallState state, bool finished)
        => !(finished || state is GameInstallState.Finish);


    /// <summary>Starward <c>Grid_Root_PointerEntered</c>：只有不在 Stop（或已完成）时才有信息可看。</summary>
    public static bool PopupOpen(GameInstallState state, bool finished, bool pointerOver)
        => pointerOver && (finished || state is not GameInstallState.Stop);


    /// <summary>Starward <c>ToBytesText</c>：≥1GB 用 GB，否则 MB，两位小数。</summary>
    public static string BytesText(long finish, long total)
    {
        const double MB = 1 << 20;
        const double GB = 1 << 30;
        return total >= GB
            ? $"{finish / GB:F2}/{total / GB:F2} GB"
            : $"{finish / MB:F2}/{total / MB:F2} MB";
    }


    /// <summary>Starward <c>ToSpeedText</c>：≥1MB/s 用 MB/s，否则 KB/s，两位小数。</summary>
    public static string SpeedText(long bytesPerSecond)
    {
        const double KB = 1 << 10;
        const double MB = 1 << 20;
        return bytesPerSecond >= MB
            ? $"{bytesPerSecond / MB:F2} MB/s"
            : $"{bytesPerSecond / KB:F2} KB/s";
    }


    /// <summary>Starward <c>ToRemainTimeText</c>：0 → <c>--:--:--</c>，否则 <c>hh:mm:ss</c>。</summary>
    public static string RemainTimeText(long seconds)
        => seconds == 0 ? "--:--:--" : TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");

}
