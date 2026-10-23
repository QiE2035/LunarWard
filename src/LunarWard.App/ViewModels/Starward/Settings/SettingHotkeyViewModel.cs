using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 快捷键页（复刻 Starward <c>HotkeySetting.xaml</c>）。
/// </summary>
/// <remarks>
/// Starward 用 Win32 <c>RegisterHotKey</c> 注册两个全局热键（显示主窗口 / 游戏截图）。
/// Linux 没有跨桌面等价 API，LunarWard <b>不注册热键</b>，也不能伪造一个输入框——
/// 这里展示桌面入口文件位置，并说明用合成器快捷键绑定的做法。
/// </remarks>
public sealed partial class SettingHotkeyViewModel : SettingSectionViewModel
{
    public SettingHotkeyViewModel(PageContext context, bool showDivider = false)
        : base(context, SettingSectionKind.Hotkey, "Settings.KeyboardShortcuts", "\uEDA7", showDivider)
    {
        DesktopEntryPath = SettingShellActions.FindDesktopEntry();
        DesktopEntryCandidates = string.Join('\n', SettingShellActions.DesktopEntryCandidates());
    }


    /// <summary>已安装的 <c>lunarward.desktop</c>，找不到为 <c>null</c>。</summary>
    public string? DesktopEntryPath { get; }

    /// <summary>候选路径（找不到时用来告诉用户该放哪）。</summary>
    public string DesktopEntryCandidates { get; }

    public bool HasDesktopEntry => DesktopEntryPath is not null;


    [RelayCommand]
    private void OpenDesktopEntry()
    {
        var path = DesktopEntryPath;
        Status = path is null
            ? Strings["Settings.Hotkey.DesktopEntryMissing"]
            : SettingShellActions.TryOpen(path) is { } error
                ? string.Format(Strings["Settings.Common.OpenFailed"], error)
                : null;
    }


    /// <summary>桌面入口内容（只读展示，等价于 Starward 的"热键已注册"信息位）。</summary>
    public string DesktopEntryPreview => """
        [Desktop Entry]
        Type=Application
        Name=LunarWard
        Exec=lunarward %U
        MimeType=x-scheme-handler/lunarward;
        Categories=Game;Utility;
        """;
}
