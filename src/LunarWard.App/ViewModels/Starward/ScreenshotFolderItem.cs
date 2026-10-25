using CommunityToolkit.Mvvm.ComponentModel;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 「管理截图文件夹」对话框里的一行。
/// </summary>
/// <remarks>
/// 对应 Starward <c>Features/Screenshot/ScreenshotFolder.cs</c>：<c>InGame</c> 的行是游戏自己的
/// 截图目录（不可移除、带「备份截图」按钮），其余是用户添加的额外目录（可移除）。
/// 备份目标在 LunarWard 里是 <c>~/Pictures/LunarWard</c>——见
/// <see cref="Views.Starward.ScreenshotFolderManageDialog"/> 的注释。
/// </remarks>
public sealed partial class ScreenshotFolderItem : ObservableObject
{
    public ScreenshotFolderItem(string folder, bool inGame)
    {
        Folder = folder;
        IsInGame = inGame;
    }


    /// <summary>绝对路径。</summary>
    public string Folder { get; }


    /// <summary>是否为游戏内截图目录（结论：不可移除）。</summary>
    public bool IsInGame { get; }


    public bool CanRemove => !IsInGame;


    /// <summary>
    /// 备份按钮只出现在游戏内目录这一行（与上游相同的可见性规则）。
    /// </summary>
    public bool CanBackup => IsInGame;

}
