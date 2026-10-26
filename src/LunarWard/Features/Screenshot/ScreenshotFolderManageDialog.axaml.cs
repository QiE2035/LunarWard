using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LunarWard.Core.HoYoPlay;
using LunarWard.Helpers;
using LunarWard.Language;
using LunarWard.Frameworks.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ScreenshotFolderManageDialog</c>：管理截图画廊里展示的文件夹。
/// </summary>
/// <remarks>
/// 平台差异（见 docs/09）：
/// <list type="number">
/// <item>上游 <c>ContentDialog</c> 由 WinUI 托管，这里继承 <see cref="StarwardContentDialog"/>，
/// 由 <c>DialogService.ShowAsync</c> 关闭。</item>
/// <item><c>Launcher.LaunchFolderPathAsync</c>（WinRT）→ <c>xdg-open &lt;folder&gt;</c>，与本地其它页面一致。</item>
/// <item><c>FileDialogHelper.PickFolderAsync(XamlRoot)</c> → <see cref="FileDialogHelper.PickFolderAsync(TopLevel?)"/>（StorageProvider）。</item>
/// <item><c>Visibility</c> → <c>IsVisible</c>；<c>Lang.Xxx</c> 静态属性 → <c>AppStrings</c> 索引器（资源键同名）。</item>
/// <item>上游 <c>Button_BackupFolder</c> 里判断 <c>ScreenshotFolders.FirstOrDefault(x =&gt; x.Default)</c> 后写
/// <c>Lang.ScreenshotPage_BackedUpNewScreenshots</c>；找不到默认文件夹时记 warning，
/// 日志文本按上游语义保留。</item>
/// </list>
/// </remarks>
public partial class ScreenshotFolderManageDialog : StarwardContentDialog
{


    public GameId CurrentGameId { get; set; } = null!;


    public List<ScreenshotFolder> Folders { get; set; } = null!;



    public ObservableCollection<ScreenshotFolder> ScreenshotFolders { get; set; } = new();


    public bool FolderChanged { get; set; }


    public bool CanSave { get; set; }



    public ScreenshotFolderManageDialog()
    {
        InitializeComponent();

        ListBox_ScreenshotFolders.ItemsSource = ScreenshotFolders;
        TextBlock_Hint.Text = Lang.ScreenshotFolderManageDialog_ImagesFromTheFollowingFoldersWillBeDisplayedInTheGallery;
        TextBlock_BackingUp.Text = Lang.ScreenshotFolderManageDialog_BackingUp;
        Button_AddFolder.Content = Lang.ScreenshotFolderManageDialog_AddFolder;
        Button_Save.Content = Lang.Common_Save;
        Button_Cancel.Content = Lang.Common_Cancel;
        Loaded += ScreenshotFolderManageDialog_Loaded;
        Unloaded += ScreenshotFolderManageDialog_Unloaded;
    }


    private void ScreenshotFolderManageDialog_Loaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Folders is not null)
            {
                foreach (ScreenshotFolder item in Folders)
                {
                    ScreenshotFolders.Add(item);
                }
            }
            ListBox_ScreenshotFolders.ContainerPrepared += ListBox_ScreenshotFolders_ContainerPrepared;
        }
        catch { }
    }



    private void ListBox_ScreenshotFolders_ContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is ListBoxItem { Content: ScreenshotFolder } item)
        {
            Button? open = item.GetVisualDescendants().OfType<Button>().FirstOrDefault(x => x.Name is "Button_OpenFolder");
            Button? remove = item.GetVisualDescendants().OfType<Button>().FirstOrDefault(x => x.Name is "Button_RemoveFolder");
            Button? backup = item.GetVisualDescendants().OfType<Button>().FirstOrDefault(x => x.Name is "Button_BackupFolder");
            if (open is not null)
            {
                ToolTip.SetTip(open, Lang.Common_Open);
            }
            if (remove is not null)
            {
                ToolTip.SetTip(remove, Lang.Common_Remove);
            }
            if (backup is not null)
            {
                ToolTip.SetTip(backup, Lang.ScreenshotPage_BackupScreenshots);
            }
        }
    }



    private void ScreenshotFolderManageDialog_Unloaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            ScreenshotFolders.Clear();
            ScreenshotFolders = null!;
        }
        catch { }
    }




    private async void Button_AddFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? folder = await FileDialogHelper.PickFolderAsync(TopLevel.GetTopLevel(this));
            if (Directory.Exists(folder))
            {
                if (!ScreenshotFolders.Any(x => x.Folder == folder))
                {
                    ScreenshotFolders.Add(new ScreenshotFolder(folder));
                    CanSave = true;
                    Button_Save.IsEnabled = true;
                }
            }
        }
        catch
        {
            // 上游只记日志；本地没有对应 Logger 实例，静默忽略（对话框保持打开）。
        }
    }




    private void Button_OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Control { DataContext: ScreenshotFolder folder })
            {
                if (Directory.Exists(folder.Folder))
                {
                    using Process process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        ArgumentList = { folder.Folder },
                        UseShellExecute = false,
                    })!;
                }
            }
        }
        catch { }
    }



    private void Button_RemoveFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Control { DataContext: ScreenshotFolder folder })
            {
                ScreenshotFolders.Remove(folder);
                CanSave = true;
                Button_Save.IsEnabled = true;
            }
        }
        catch { }
    }



    private async void Button_BackupFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.IsEnabled = false;
            try
            {
                TextBlock_BackupResult.IsVisible = false;
                if (button.DataContext is ScreenshotFolder folder)
                {
                    if (Directory.Exists(folder.Folder))
                    {
                        if (ScreenshotFolders.FirstOrDefault(x => x.Default) is ScreenshotFolder screenshotFolder)
                        {
                            string backupFolder = screenshotFolder.Folder;
                            Directory.CreateDirectory(backupFolder);
                            StackPanel_BackingUp.IsVisible = true;
                            int count = await Task.Run(() =>
                            {
                                int count = 0;
                                string[] files = Directory.GetFiles(folder.Folder);
                                foreach (string item in files)
                                {
                                    string target = Path.Combine(backupFolder, Path.GetFileName(item));
                                    if (!File.Exists(target))
                                    {
                                        File.Copy(item, target);
                                        count++;
                                    }
                                }
                                return count;
                            });
                            StackPanel_BackingUp.IsVisible = false;
                            TextBlock_BackupResult.IsVisible = true;
                            TextBlock_BackupResult.Text = string.Format(Lang.ScreenshotPage_BackedUpNewScreenshots, count);
                            return;
                        }
                    }
                    TextBlock_BackupResult.IsVisible = true;
                    TextBlock_BackupResult.Text = Lang.ScreenshotFolderManageDialog_FailedToBackupScreenshots;
                    return;
                }
            }
            catch
            {
                TextBlock_BackupResult.IsVisible = true;
                TextBlock_BackupResult.Text = Lang.ScreenshotFolderManageDialog_FailedToBackupScreenshots;
            }
            finally
            {
                StackPanel_BackingUp.IsVisible = false;
                button.IsEnabled = true;
            }
        }
    }




    private void Button_Save_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            FolderChanged = true;
            Folders ??= new();
            Folders.Clear();
            Folders.AddRange(ScreenshotFolders.Where(x => x.CanRemove));
            RequestClose();
        }
        catch
        {
            RequestClose();
        }
    }



    private void Button_Cancel_Click(object? sender, RoutedEventArgs e)
    {
        RequestClose();
    }


}
