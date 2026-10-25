using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.App.Localization;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 「管理截图文件夹」对话框的状态。
/// </summary>
/// <remarks>
/// 对应 Starward <c>ScreenshotFolderManageDialog.xaml.cs</c>：列表是「游戏截图目录 + 额外目录」，
/// 额外目录才能移除，保存时把额外目录写回配置（上游是 <c>SetExternalScreenshotFolder</c>，
/// LunarWard 落在 <c>ProfileConfig.ScreenshotFolders</c>）。
/// </remarks>
public sealed partial class ScreenshotFolderManageDialogViewModel : ObservableObject
{
    public ScreenshotFolderManageDialogViewModel(string inGameDirectory, IEnumerable<string> externalFolders)
    {
        InGameDirectory = inGameDirectory;
        Folders.Add(new ScreenshotFolderItem(inGameDirectory, inGame: true));
        foreach (string folder in externalFolders)
        {
            if (!string.IsNullOrWhiteSpace(folder) && Folders.All(x => x.Folder != folder))
            {
                Folders.Add(new ScreenshotFolderItem(folder, inGame: false));
            }
        }
    }


    public AppStrings Strings => AppStrings.Instance;


    /// <summary>游戏自己的截图目录（不可移除，备份的目标目录）。</summary>
    public string InGameDirectory { get; }


    public ObservableCollection<ScreenshotFolderItem> Folders { get; } = [];


    /// <summary>保存按钮是否可用（源码 <c>CanSave</c>：改动过才启用）。</summary>
    [ObservableProperty]
    private bool _canSave;


    /// <summary>备份中（源码 <c>StackPanel_BackingUp</c>）。</summary>
    [ObservableProperty]
    private bool _isBackingUp;


    /// <summary>备份结果文字（源码 <c>TextBlock_BackupResult</c>），null 表示不显示。</summary>
    [ObservableProperty]
    private string? _backupResult;


    /// <summary>保存后的额外目录列表；取消时为 null。</summary>
    public IReadOnlyList<string>? Result { get; private set; }


    public void Add(string folder)
    {
        if (Folders.All(x => x.Folder != folder))
        {
            Folders.Add(new ScreenshotFolderItem(folder, inGame: false));
            CanSave = true;
        }
    }


    public void Remove(ScreenshotFolderItem item)
    {
        if (item.CanRemove && Folders.Remove(item))
        {
            CanSave = true;
        }
    }


    public void Save()
    {
        Result = Folders.Where(x => x.CanRemove).Select(x => x.Folder).ToList();
    }

}
