using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward.Settings;

public partial class SettingFileManageView : SettingSectionView
{
    private readonly SettingFileManageViewModel _viewModel;


    public SettingFileManageView(SettingFileManageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        viewModel.BackupRequested += OnBackupRequested;
    }


    /// <summary>
    /// 「备份数据库」：弹保存对话框，再把整库快照写到用户选的路径
    /// （模拟上游 <c>FileManageSetting</c> 的 FileSavePicker + File.Copy）。
    /// </summary>
    private async void OnBackupRequested(object? sender, EventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is not { CanSave: true } storage)
        {
            return;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = _viewModel.Strings["Settings.Files.BackupDatabase"],
            SuggestedFileName = $"LunarWard_{DateTime.Now:yyyyMMdd_HHmmss}.db",
            DefaultExtension = "db",
            FileTypeChoices = [new FilePickerFileType("SQLite") { Patterns = ["*.db"] }],
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            await _viewModel.BackupToAsync(path);
        }
    }
}
