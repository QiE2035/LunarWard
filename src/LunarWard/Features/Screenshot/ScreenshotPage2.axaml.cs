using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ScreenshotPage2</c> 的 1:1 移植：游戏截图画廊（磁贴墙 + 年月标签）。
/// </summary>
/// <remarks>
/// 平台差异（全部登记 docs/09）：
/// <list type="number">
/// <item><c>OnNavigatedTo(NavigationEventArgs)</c> → <see cref="PageBase.OnNavigatedTo(object?)"/>；
/// <c>BitmapImage(AppConfig.EmojiXxx)</c> → <c>new Bitmap(uri.LocalPath)</c>（本地表情是文件路径 Uri）。</item>
/// <item><c>ItemsView.ScrollView.ViewportHeight/ExtentHeight</c> → <c>ScrollViewer.Viewport.Height/Extent.Height</c>；
/// <c>VerticalScrollController</c> → <see cref="ScrollBar"/> 双向同步（见 <see cref="UpdateLabels"/>）。</item>
/// <item><c>AnnotatedScrollBar</c>（含 <c>DetailLabelRequested</c>/标签轨道/<c>PART_ToolTipRail</c> 悬浮提示）
/// 在 Avalonia 无等价控件：保留「内容超过 3 屏才显示滚动条」与年份分段计算，标签轨道与 tooltip 省略。</item>
/// <item><c>DispatcherQueue.TryEnqueue</c> → <c>Dispatcher.UIThread.Post</c>。</item>
/// <item><c>StorageFile</c>/<c>DataPackage</c> 复制、拖拽 → <c>TopLevel.StorageProvider</c> + <c>Clipboard.SetFilesAsync</c> + <c>DragDrop.DoDragDrop</c>。</item>
/// <item><c>ImageViewWindow2.ShowWindowAsync(AppWindowId, item, list)</c> → <c>new ImageViewWindow2(item, list).Show(owner)</c>
/// （本地无 AppWindow 直连，窗口由 Avalonia 生命周期管理）。</item>
/// </list>
/// </remarks>
public partial class ScreenshotPage2 : PageBase
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


    public AppStrings Strings => AppStrings.Instance;


    private readonly HoYoPlayService _hoyoplayService = AppConfig.GetService<HoYoPlayService>();



    public ScreenshotPage2()
    {
        InitializeComponent();
        DataContext = this;
    }



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
        await InitializeAsync();
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
            Screenshots = null!;
            ScrollViewer_Images.ScrollChanged -= ScrollViewer_Images_ScrollChanged;
            ScrollBar_Images.PropertyChanged -= ScrollBar_Images_PropertyChanged;
            ScrollBar_Images = null!;
        }
        catch { }
    }



    private List<FileSystemWatcher> _watchers = new();

    private List<ScreenshotFolder> _folders = new();

    private Dictionary<string, ScreenshotItem> _screenshotDict = new();

    public ObservableCollection<ScreenshotItem> Screenshots { get; set => SetProperty(ref field, value); }



    private async Task InitializeAsync()
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
            Screenshots = null!;

            (string? backupFolder, string? screenshotFolder) = await GetGameScreenshotPathAsync();
            if (backupFolder is not null)
            {
                string folder = Path.GetFullPath(backupFolder);
                if (_folders.FirstOrDefault(x => x.Folder == folder) is null)
                {
                    var watcher = CreateFileSystemWatcher(folder);
                    _watchers.Add(watcher);
                    _folders.Add(new(folder) { Default = true });
                }
            }
            if (screenshotFolder is not null)
            {
                string folder = Path.GetFullPath(screenshotFolder);
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
                    if (ScreenshotHelper.IsSupportedExtension(file) && !File.GetAttributes(file).HasFlag((System.IO.FileAttributes)0x440000))
                    {
                        var item = new ScreenshotItem(file);
                        screenshots.Add(item);
                        _screenshotDict[name] = item;
                    }
                }
            }

            var list = screenshots.OrderByDescending(x => x.CreationTime).ToList();
            Screenshots = new(list);
            await Task.Delay(100);
            UpdateLabels();
        }
        catch (Exception ex)
        {
            // 上游 _logger.LogError(ex, "Initialize")；本地页面级别没有注入 ILogger，用全局 logger。
            AppConfig.GetLogger<ScreenshotPage2>().LogError(ex, "Initialize");
        }
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
            AppConfig.GetLogger<ScreenshotPage2>().LogError(ex, "Get game screenshot path");
        }
        return (null, null);
    }



    /// <summary>
    /// 上游 <c>UpdateLabels</c>：内容超过 3 屏时显示 <c>AnnotatedScrollBar</c> 并计算年/月标签。
    /// Avalonia 无 <c>AnnotatedScrollBar</c>，这里保留显隐判定与滚动条同步，标签集合的绘制省略（见 docs/09）。
    /// </summary>
    private void UpdateLabels()
    {
        try
        {
            if (Screenshots?.Count > 0)
            {
                double viewportHeight = ScrollViewer_Images.Viewport.Height;
                double extentHeight = ScrollViewer_Images.Extent.Height;
                if (viewportHeight > 0 && extentHeight / viewportHeight > 3)
                {
                    ScrollViewer_Images.ScrollChanged -= ScrollViewer_Images_ScrollChanged;
                    ScrollViewer_Images.ScrollChanged += ScrollViewer_Images_ScrollChanged;
                    ScrollBar_Images.PropertyChanged -= ScrollBar_Images_PropertyChanged;
                    ScrollBar_Images.PropertyChanged += ScrollBar_Images_PropertyChanged;
                    ScrollBar_Images.IsVisible = true;
                    SyncScrollBar();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            AppConfig.GetLogger<ScreenshotPage2>().LogError(ex, "Update labels");
        }
        ScrollBar_Images.IsVisible = false;
    }



    private bool _syncingScrollBar;



    private void SyncScrollBar()
    {
        if (_syncingScrollBar)
        {
            return;
        }
        _syncingScrollBar = true;
        ScrollBar_Images.Minimum = 0;
        ScrollBar_Images.Maximum = Math.Max(0, ScrollViewer_Images.Extent.Height - ScrollViewer_Images.Viewport.Height);
        ScrollBar_Images.ViewportSize = ScrollViewer_Images.Viewport.Height;
        ScrollBar_Images.Value = ScrollViewer_Images.Offset.Y;
        ScrollBar_Images.LargeChange = Math.Max(1, ScrollViewer_Images.Viewport.Height);
        _syncingScrollBar = false;
    }



    private void ScrollViewer_Images_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        SyncScrollBar();
    }



    private void ScrollBar_Images_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollBar.ValueProperty && !_syncingScrollBar)
        {
            ScrollViewer_Images.Offset = new Vector(ScrollViewer_Images.Offset.X, ScrollBar_Images.Value);
        }
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
                        Screenshots ??= new();
                        Screenshots.Insert(0, item);
                        UpdateLabels();
                    });
                }
            }
        }
        catch (Exception ex)
        {
            AppConfig.GetLogger<ScreenshotPage2>().LogError(ex, "File system watcher created event");
        }
    }



    private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (Screenshots?.FirstOrDefault(x => x.FilePath == e.FullPath) is ScreenshotItem item)
            {
                _screenshotDict.Remove(Path.GetFileName(e.FullPath));
                Dispatcher.UIThread.Post(() => Screenshots?.Remove(item));
                UpdateLabels();
            }
        }
        catch { }
    }





    #region Action


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
                await InitializeAsync();
            }
        }
        catch { }
    }



    private async void Button_CopyImage_Tapped(object? sender, TappedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button button)
        {
            if (button.DataContext is ScreenshotItem item)
            {
                try
                {
                    AppConfig.GetLogger<ScreenshotPage2>().LogInformation("Copy image: {file}", item.FilePath);
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel is not null)
                    {
                        // 上游 ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, file)：这里把文件放进剪贴板。
                        var storageFile = await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(item.FilePath));
                        if (storageFile is not null)
                        {
                            await topLevel.Clipboard!.SetFilesAsync([storageFile]);
                        }
                        button.Content = new TextBlock { Text = "\uE8FB", FontSize = 16 };
                        await Task.Delay(3000);
                        button.Content = new TextBlock { Text = "\uE8C8", FontSize = 16 };
                    }
                }
                catch (Exception ex)
                {
                    AppConfig.GetLogger<ScreenshotPage2>().LogError(ex, "Copy image");
                }
            }
        }
    }



    private void Grid_ImageItem_Tapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (sender is Control grid && grid.DataContext is ScreenshotItem item)
            {
                var window = new ImageViewWindow2(item, Screenshots);
                if (TopLevel.GetTopLevel(this) is Window owner)
                {
                    window.Show(owner);
                }
                else
                {
                    window.Show();
                }
            }
        }
        catch (Exception ex)
        {
            AppConfig.GetLogger<ScreenshotPage2>().LogError(ex, "Open ImageViewWindow");
        }
    }



    private Point _dragStartPoint;

    private PointerPressedEventArgs? _dragPress;

    private bool _dragArmed;



    private void Grid_ImageItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 上游用 DragStarting 事件（WinUI 自带拖拽手势识别）；
        // Avalonia 需要自己按住移动一小段距离后调用 DragDrop.DoDragDrop。
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(this);
            _dragPress = e;
            _dragArmed = true;
        }
    }



    private async void Grid_ImageItem_PointerMoved(object? sender, PointerEventArgs e)
    {
        try
        {
            if (!_dragArmed || sender is not Control grid || grid.DataContext is not ScreenshotItem item)
            {
                return;
            }
            Point point = e.GetPosition(this);
            if (Math.Abs(point.X - _dragStartPoint.X) < 8 && Math.Abs(point.Y - _dragStartPoint.Y) < 8)
            {
                return;
            }
            _dragArmed = false;
            var topLevel = TopLevel.GetTopLevel(this);
            var storageFile = topLevel is null ? null : await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(item.FilePath));
            if (storageFile is not null && _dragPress is not null)
            {
                var transfer = new DataTransfer();
                transfer.Add(DataTransferItem.CreateFile(storageFile));
                await DragDrop.DoDragDropAsync(_dragPress, transfer, DragDropEffects.Copy);
            }
        }
        catch (Exception ex)
        {
            AppConfig.GetLogger<ScreenshotPage2>().LogError(ex, "Drag image starting");
        }
    }



    #endregion


}
