using LunarWard.ViewModels.Starward;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 「管理截图文件夹」对话框的状态（对齐 Starward <c>ScreenshotFolderManageDialog</c> 的 CanSave/保存语义）。
/// </summary>
public class ScreenshotFolderManageDialogTests
{

    [Fact]
    public void InGameFolderIsFirstAndCannotBeRemoved()
    {
        var vm = new ScreenshotFolderManageDialogViewModel("/game/ScreenShot", []);

        var row = Assert.Single(vm.Folders);
        Assert.Equal("/game/ScreenShot", row.Folder);
        Assert.True(row.IsInGame);
        Assert.False(row.CanRemove);
        Assert.True(row.CanBackup);
    }


    [Fact]
    public void ExternalFoldersAreRemovableAndNotBackupTargets()
    {
        var vm = new ScreenshotFolderManageDialogViewModel("/game/ScreenShot", ["/a", "/b"]);

        Assert.Equal(3, vm.Folders.Count);
        Assert.True(vm.Folders[1].CanRemove);
        Assert.False(vm.Folders[1].CanBackup);
    }


    [Fact]
    public void DuplicateFoldersAreIgnored()
    {
        var vm = new ScreenshotFolderManageDialogViewModel("/game/ScreenShot", ["/a", "/a", ""]);

        Assert.Equal(2, vm.Folders.Count);
        vm.Add("/a");
        Assert.Equal(2, vm.Folders.Count);
    }


    [Fact]
    public void CanSaveOnlyAfterChangeAndResultSkipsInGameFolder()
    {
        var vm = new ScreenshotFolderManageDialogViewModel("/game/ScreenShot", ["/a"]);
        Assert.False(vm.CanSave);

        vm.Add("/b");
        Assert.True(vm.CanSave);

        vm.Save();
        Assert.Equal(["/a", "/b"], vm.Result);

        var empty = new ScreenshotFolderManageDialogViewModel("/game/ScreenShot", []);
        empty.Save();
        Assert.Empty(empty.Result!);
    }


    [Fact]
    public void RemoveDropsTheRowAndMarksDirty()
    {
        var vm = new ScreenshotFolderManageDialogViewModel("/game/ScreenShot", ["/a", "/b"]);

        vm.Remove(vm.Folders[1]);
        Assert.False(vm.CanSave is false);
        Assert.Equal(["/b"], vm.Folders.Where(x => x.CanRemove).Select(x => x.Folder));

        // 游戏内目录不可移除
        vm.Remove(vm.Folders[0]);
        Assert.Equal(2, vm.Folders.Count);
    }

}
