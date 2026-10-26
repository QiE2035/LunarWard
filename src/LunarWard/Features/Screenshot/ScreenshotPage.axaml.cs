using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using LunarWard.Core;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.HoYoPlay;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using LunarWard.Localization;
using LunarWard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using LunarWard.Language;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ScreenshotPage</c>（XAML 274 行 + code-behind 695 行）的移植：
/// 游戏截图画廊（按「月-日」分组的磁贴墙、多选、右键菜单、批量转换入口）。
/// </summary>
/// <remarks>
/// 平台差异（docs/09 §8.3）：
/// <list type="number">
/// <item>分组 <c>GridView</c> + <c>GroupStyle</c> + <c>ItemsWrapGrid</c>（sticky group headers）→
/// 双层 <c>ItemsControl</c>（外层分组、内层 <c>WrapPanel</c>），分组头由模板绘制，无吸顶。</item>
/// <item>悬停 <c>Storyboard</c> + <c>mxi:Interaction</c> → XAML <c>Style :pointerover</c> + <c>Transitions</c>。</item>
/// <item><c>GridView.SelectionMode</c> 的 <c>Multiple</c>/<c>None</c> 切换 → 自维护选中集合（条目点击切换 <c>selected</c> 类），
/// 计数文本仍按上游 <c>"{n}/{total}"</c> 拼接。</item>
/// <item><c>DragItemsStarting</c> → <c>PointerPressed</c>/<c>PointerMoved</c> + <c>DragDrop.DoDragDrop</c>；
/// <c>StorageFile</c>/<c>DataPackage</c> → <c>StorageProvider</c> + <c>DataObject(DataFormats.Files)</c>。</item>
/// <item><c>ClipboardHelper.SetStorageItems</c>/<c>SetBitmap</c> → <c>TopLevel.Clipboard.SetFilesAsync</c> 与
/// <c>ClipboardHelper.SetBitmap</c>（截图转 JPG 后）。</item>
/// <item><c>ImageBatchConvertWindow</c> 尚未移植（docs/09 §8.4）：命令保留，点击给出提示。</item>
/// </list>
/// </remarks>
public partial class ScreenshotPage : PageBase
{

    public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;


    protected bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));
        return true;
    }


    private readonly ILogger<ScreenshotPage> _logger = AppConfig.GetLogger<ScreenshotPage>();

    private readonly HoYoPlayService _hoyoplayService = AppConfig.GetService<HoYoPlayService>();


    public AppStrings Strings => AppStrings.Instance;



    public ScreenshotPage()
    {
        InitializeComponent();
        DataContext = this;
    }



    public bool MutliSelect
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (!value)
                {
                    ClearSelection();
                }
                UpdateSelectCountText();
            }
        }
    }


    public string SelectCountText { get; set => SetProperty(ref field, value); } = string.Empty;



    private List<FileSystemWatcher> _watchers = new();

    private List<ScreenshotFolder> _folders = new();

    private Dictionary<string, ScreenshotItem> _screenshotDict = new();

    private List<ScreenshotItem>? _screenshotItems;

    public ObservableCollection<ScreenshotItemGroup> ScreenshotGroups { get; set => SetProperty(ref field, value); } = new();

    private readonly HashSet<ScreenshotItem> _selectedItems = new();



    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        Image_Emoji.Source = CurrentGameBiz.ToGame().Value switch
        {
            GameBiz.hk4e => new Bitmap(AppConfig.EmojiPaimon.LocalPath),
            GameBiz.hkrpg => new Bitmap(AppConfig.EmojiPom.LocalPath),
            GameBiz.bh3 => new Bitmap(AppConfig.EmojiAI.LocalPath),
            GameBiz.nap => new Bitmap(AppConfig.EmojiBangboo.LocalPath),
            _ => null,
        };
    }



    protected override async void OnLoaded()
    {
        await Task.Delay(16);
        Initialize();
    }



    protected override void OnUnloaded()
    {
        try
        {
            foreach (var item in _watchers)
            {
                item.Dispose();
            }
            _watchers.Clear();
            _folders.Clear();
            _folders = null!;
            _screenshotDict.Clear();
            _screenshotDict = null!;
            _screenshotItems = null;
            _selectedItems.Clear();
            ScreenshotGroups = null!;
        }
        catch { }
    }



    private async void Initialize()
    {
        try
        {
            foreach (var item in _watchers)
            {
                item.Dispose();
            }
            _watchers.Clear();
            _folders.Clear();
            _screenshotDict.Clear();
            ScreenshotGroups = new();

            (string? defaultFolder, string? inGameFolder) = await GetGameScreenshotPathAsync();
            if (defaultFolder is not null)
            {
                string folder = Path.GetFullPath(defaultFolder);
                if (_folders.FirstOrDefault(x => x.Folder == folder) is null)
                {
                    var watcher = CreateFileSystemWatcher(folder);
                    _watchers.Add(watcher);
                    _folders.Add(new(folder) { Default = true });
                }
            }
            if (inGameFolder is not null)
            {
                string folder = Path.GetFullPath(inGameFolder);
                if (_folders.FirstOrDefault(x => x.Folder == folder) is null)
                {
                    var watcher = CreateFileSystemWatcher(folder);
                    _watchers.Add(watcher);
                    _folders.Add(new(folder) { InGame = true });
                }
            }

            string? externalFolder = AppConfig.GetExternalScreenshotFolder(CurrentGameBiz);
            if (!string.IsNullOrWhiteSpace(externalFolder))
            {
                foreach (var item in externalFolder.Split(';'))
                {
                    string folder = item.Trim();
                    if (Directory.Exists(folder))
                    {
                        folder = Path.GetFullPath(folder);
                        if (_folders.FirstOrDefault(x => x.Folder == folder) is null)
                        {
                            var watcher = CreateFileSystemWatcher(folder);
                            _watchers.Add(watcher);
                            _folders.Add(new(folder));
                        }
                    }
                }
            }

            List<ScreenshotItem> screenshots = new();
            foreach (var folderItem in _folders)
            {
                var files = Directory.GetFiles(folderItem.Folder);
                foreach (var file in files)
                {
                    string name = Path.GetFileName(file);
                    if (_screenshotDict.ContainsKey(name))
                    {
                        continue;
                    }
                    // 上游此处有 todo（优化加载速度），且不按 FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS 过滤，逐字保留。
                    if (ScreenshotHelper.IsSupportedExtension(file))
                    {
                        var item = new ScreenshotItem(file);
                        screenshots.Add(item);
                        _screenshotDict[name] = item;
                    }
                }
            }

            _screenshotItems = new(screenshots.OrderByDescending(x => x.CreationTime).ToList());
            var groups = _screenshotItems.GroupBy(x => x.TimeMonthDay).Select(x => new ScreenshotItemGroup(x.Key, x)).ToList();
            ScreenshotGroups = new(groups);
            StackPanel_Emoji.IsVisible = groups.Count == 0;
            UpdateSelectCountText();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize");
        }
    }



    public async Task<(string? BackupFolder, string? ScreenshotFolder)> GetGameScreenshotPathAsync()
    {
        try
        {
            string? backupFolder = null, screenshotFolder = null;
            string? name = GameLauncherService.GetGameExeName(CurrentGameBiz)?.Replace(".exe", "");
            string? relativePath = CurrentGameBiz.Game switch
            {
                GameBiz.hk4e => "ScreenShot",
                GameBiz.hkrpg => @"StarRail_Data\ScreenShots",
                GameBiz.bh3 => @"ScreenShot",
                GameBiz.nap => @"ScreenShot",
                _ => null,
            };
            if (name is null || relativePath is null)
            {
                var config = await _hoyoplayService.GetGameConfigAsync(CurrentGameId);
                if (config is not null)
                {
                    name ??= config.ExeFileName.Replace(".exe", "");
                    relativePath ??= config.GameScreenshotDir;
                }
            }
            string? folder = AppConfig.ScreenshotFolder;
            if (!Directory.Exists(folder))
            {
                folder = Path.Join(AppConfig.UserDataFolder, "Screenshots");
            }
            folder = Path.Join(folder, name);
            Directory.CreateDirectory(folder);
            backupFolder = folder;
            string? installPath = GameLauncherService.GetGameInstallPath(CurrentGameId);
            folder = Path.Join(installPath, relativePath);
            if (Directory.Exists(folder))
            {
                screenshotFolder = folder;
            }
            return (backupFolder, screenshotFolder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get game screenshot path");
        }
        return (null, null);
    }



    private FileSystemWatcher CreateFileSystemWatcher(string folder)
    {
        var watcher = new FileSystemWatcher(folder);
        watcher.NotifyFilter = NotifyFilters.FileName;
        watcher.Filters.Add("*.jpg");
        watcher.Filters.Add("*.png");
        watcher.Filters.Add("*.jxr");
        watcher.Created += FileSystemWatcher_Created;
        watcher.Deleted += FileSystemWatcher_Deleted;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }



    private async void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                string name = Path.GetFileName(e.FullPath);
                if (_screenshotDict.ContainsKey(name))
                {
                    return;
                }
                if (ScreenshotHelper.IsSupportedExtension(e.FullPath) && File.Exists(e.FullPath))
                {
                    await ScreenshotHelper.WaitForFileReleaseAsync(e.FullPath, CancellationToken.None);
                    var item = new ScreenshotItem(e.FullPath);
                    _screenshotDict[name] = item;
                    Dispatcher.UIThread.Post(() =>
                    {
                        _screenshotItems?.Insert(0, item);
                        if (_screenshotItems is not null)
                        {
                            var groups = _screenshotItems.GroupBy(x => x.TimeMonthDay).Select(x => new ScreenshotItemGroup(x.Key, x)).ToList();
                            ScreenshotGroups = new(groups);
                            StackPanel_Emoji.IsVisible = false;
                            UpdateSelectCountText();
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File system watcher created event");
        }
    }



    private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (_screenshotItems?.FirstOrDefault(x => x.FilePath == e.FullPath) is ScreenshotItem item)
            {
                _screenshotDict.Remove(Path.GetFileName(e.FullPath));
                Dispatcher.UIThread.Post(() =>
                {
                    _screenshotItems.Remove(item);
                    _selectedItems.Remove(item);
                    var groups = _screenshotItems.GroupBy(x => x.TimeMonthDay).Select(x => new ScreenshotItemGroup(x.Key, x)).ToList();
                    ScreenshotGroups = new(groups);
                    StackPanel_Emoji.IsVisible = groups.Count == 0;
                    UpdateSelectCountText();
                });
            }
        }
        catch { }
    }





    #region Action


    [RelayCommand]
    private void OpenImageBatchConvertWindow()
    {
        // 上游：new ImageBatchConvertWindow().ShowWindow(...)。该窗口依赖 JxlSharp/ImageSharp/WIC 编码器，
        // 尚未移植（docs/09 §8.4），此处先给出明确提示而不是静默无效。
        InAppToast.MainWindow?.Information(Lang.ScreenshotPage_BatchConvert, "ImageBatchConvertWindow 尚未移植");
    }



    [RelayCommand]
    private async Task ManageScreenshotFolderAsync()
    {
        try
        {
            var dialog = new ScreenshotFolderManageDialog
            {
                Folders = _folders,
                CurrentGameId = this.CurrentGameId,
            };
            await DialogService.ShowAsync(dialog, this);
            if (dialog.FolderChanged)
            {
                string folder = string.Join(';', dialog.Folders.Where(x => x.CanRemove).Select(x => x.Folder));
                AppConfig.SetExternalScreenshotFolder(CurrentGameBiz, folder);
                Initialize();
                UpdateSelectCountText();
            }
        }
        catch { }
    }



    private void UpdateSelectCountText()
    {
        try
        {
            if (MutliSelect)
            {
                SelectCountText = $"{_selectedItems.Count}/{_screenshotItems?.Count ?? 0}";
            }
            else
            {
                SelectCountText = $"{_screenshotItems?.Count ?? 0}";
            }
            TextBlock_SelectCount.Text = SelectCountText;
        }
        catch { }
    }



    private void ClearSelection()
    {
        _selectedItems.Clear();
        foreach (var border in this.GetVisualDescendants().OfType<Border>().Where(x => x.Classes.Contains("thumb-host")))
        {
            border.Classes.Remove("selected");
        }
    }



    private void ToggleSelect(Border host, ScreenshotItem item)
    {
        if (_selectedItems.Contains(item))
        {
            _selectedItems.Remove(item);
            host.Classes.Remove("selected");
        }
        else
        {
            _selectedItems.Add(item);
            host.Classes.Add("selected");
        }
        UpdateSelectCountText();
    }



    private void Grid_ImageItem_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        // 上游 MenuFlyoutItem_ScreenshotInfo_Loading：右键菜单展开时补上像素尺寸（异步，先读文件再填 FileInfo）。
        if (sender is Border { DataContext: ScreenshotItem item })
        {
            item.UpdatePixelSize();
        }
    }



    private void Grid_ImageItem_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { DataContext: ScreenshotItem item })
        {
            OpenViewer(item);
            e.Handled = true;
        }
    }



    private void OpenViewer(ScreenshotItem item)
    {
        try
        {
            var collection = _screenshotItems is null ? null : new ObservableCollection<ScreenshotItem>(_screenshotItems);
            var window = new ImageViewWindow2(item, collection);
            if (TopLevel.GetTopLevel(this) is Window owner)
            {
                window.Show(owner);
            }
            else
            {
                window.Show();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open ImageViewWindow2");
        }
    }



    private void Grid_ImageItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 上游 GridView_Images_ItemClick：多重选择模式下点击切换选中，否则打开查看器。
        // Avalonia 的 ItemsControl 没有 ItemClick，这里在条目上处理。
        if (sender is not Border host || host.DataContext is not ScreenshotItem item)
        {
            return;
        }
        var point = e.GetCurrentPoint(host);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragStart = e.GetPosition(this);
        _dragPress = e;
        _dragItem = item;
        _dragArmed = true;
        if (e.ClickCount >= 2)
        {
            return;
        }
        if (MutliSelect)
        {
            ToggleSelect(host, item);
            e.Handled = true;
        }
        else
        {
            OpenViewer(item);
            e.Handled = true;
        }
    }



    private Point _dragStart;

    private PointerPressedEventArgs? _dragPress;

    private ScreenshotItem? _dragItem;

    private bool _dragArmed;



    private async void Grid_ImageItem_PointerMoved(object? sender, PointerEventArgs e)
    {
        try
        {
            if (!_dragArmed || _dragItem is null)
            {
                return;
            }
            Point point = e.GetPosition(this);
            if (Math.Abs(point.X - _dragStart.X) < 8 && Math.Abs(point.Y - _dragStart.Y) < 8)
            {
                return;
            }
            _dragArmed = false;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }
            var files = new List<Avalonia.Platform.Storage.IStorageFile>();
            IEnumerable<ScreenshotItem> dragItems = _selectedItems.Count > 0 ? _selectedItems : new[] { _dragItem };
            foreach (var item in dragItems)
            {
                if (File.Exists(item.FilePath))
                {
                    var file = await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(item.FilePath));
                    if (file is not null)
                    {
                        files.Add(file);
                    }
                }
            }
            if (files.Count > 0 && _dragPress is not null)
            {
                var transfer = new DataTransfer();
                foreach (var file in files)
                {
                    transfer.Add(DataTransferItem.CreateFile(file));
                }
                await DragDrop.DoDragDropAsync(_dragPress, transfer, DragDropEffects.Copy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drag image starting");
        }
    }



    private void MenuFlyoutItem_Open_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: ScreenshotItem item })
        {
            OpenViewer(item);
        }
    }



    private async void MenuFlyoutItem_CopyFile_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem { DataContext: ScreenshotItem item } && TopLevel.GetTopLevel(this) is { } topLevel)
            {
                var file = await topLevel.StorageProvider.TryGetFileFromPathAsync(item.FilePath);
                if (file is not null)
                {
                    await topLevel.Clipboard!.SetFilesAsync([file]);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy file");
        }
    }



    private async void MenuFlyoutItem_CopyAsJPG_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // 上游走 WIC/AVIF 解码再转 JPG；Linux 侧复用 ScreenshotHelper.ConvertToJpgAsync（ffmpeg/magick 后端）。
            if (sender is MenuItem { DataContext: ScreenshotItem item })
            {
                string? jpg = await ScreenshotHelper.ConvertToJpgAsync(item.FilePath);
                if (jpg is not null)
                {
                    // 本地 ClipboardHelper 只有同步的 SetBitmap(filePath)，与上游 SetBitmap 对齐。
                    ClipboardHelper.SetBitmap(jpg);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy image as JPG");
        }
    }



    private void MenuFlyoutItem_OpenInExplorer_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem { DataContext: ScreenshotItem item })
            {
                using Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { Path.GetDirectoryName(item.FilePath)! },
                    UseShellExecute = false,
                })!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open in explorer");
        }
    }



    private void MenuFlyoutItem_OpenWithDefault_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem { DataContext: ScreenshotItem item })
            {
                using Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { item.FilePath },
                    UseShellExecute = false,
                })!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open with default app");
        }
    }



    private void MenuFlyoutItem_OpenWith_Click(object? sender, RoutedEventArgs e)
    {
        // Windows 的「打开方式」对话框（SHOpenWithDialog）在 Linux 无等价值；
        // 上游行为是让用户选一个程序打开当前文件，这里退化为用默认应用打开并给出提示。
        if (sender is MenuItem { DataContext: ScreenshotItem item })
        {
            InAppToast.MainWindow?.Information(Lang.ImageViewWindow2_OpenWith, item.FileName);
            MenuFlyoutItem_OpenWithDefault_Click(sender, e);
        }
    }



    private async void MenuFlyoutItem_Delete_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem { DataContext: ScreenshotItem item } && _screenshotItems is not null)
            {
                // 复刻上游：直接删除文件（上游在 WinUI 侧无二次确认，删除后同步集合与选中态）。
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }
                _screenshotDict.Remove(Path.GetFileName(item.FilePath));
                _screenshotItems.Remove(item);
                _selectedItems.Remove(item);
                var groups = _screenshotItems.GroupBy(x => x.TimeMonthDay).Select(x => new ScreenshotItemGroup(x.Key, x)).ToList();
                ScreenshotGroups = new(groups);
                StackPanel_Emoji.IsVisible = groups.Count == 0;
                UpdateSelectCountText();
            }
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete screenshot");
        }
    }


    #endregion


}
