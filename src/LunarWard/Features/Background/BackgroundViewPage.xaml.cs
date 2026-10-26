using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LunarWard.Helpers;
using LunarWard.Language;
using Microsoft.Extensions.Logging;

namespace LunarWard.Features.Background;

/// <summary>
/// 背景图库页面（对应上游 <c>Starward/Features/Background/BackgroundViewPage.xaml.cs</c>，411 行）。
/// </summary>
/// <remarks>
/// <para>上游是 <c>PageBase</c>（属性通知 + Loaded/Unloaded 钩子）；Avalonia 下改为
/// <see cref="UserControl"/> + 手写 <see cref="INotifyPropertyChanged"/>，钩子用 Loaded/Unloaded 事件。</para>
/// <para>平台替换：
/// <list type="bullet">
/// <item><c>StorageFile</c>/<c>Launcher</c>/<c>FolderLauncherOptions</c> → <c>Process</c>（xdg-open）+ 文件系统 API。</item>
/// <item><c>ClipboardHelper.SetStorageItems</c> → 剪贴板写入文件路径文本（多文件按行），登记于 docs/09。</item>
/// <item><c>DragItemsStarting</c> 拖拽导出未移植（Avalonia 跨进程文件拖拽需 platform drag 实现），登记于 docs/09。</item>
/// <item><c>RadioMenuFlyoutItem</c> → Avalonia <c>MenuItem</c>（ToggleType=Radio）。</item>
/// </list>
/// </para>
/// </remarks>
public partial class BackgroundViewPage : UserControl, INotifyPropertyChanged
{

    private readonly ILogger<BackgroundViewPage> _logger = AppConfig.GetLogger<BackgroundViewPage>();


    public new event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        return false;
    }


    public BackgroundViewPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadBackgroundItems();
        Unloaded += OnUnloaded;
    }


    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            BackgroundImages?.Clear();
            BackgroundImages = null!;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    private ObservableCollection<BackgroundFileItem> _backgroundImages = [];

    public ObservableCollection<BackgroundFileItem> BackgroundImages
    {
        get => _backgroundImages;
        set => SetProperty(ref _backgroundImages, value);
    }


    private string _selectCountText = "0";

    public string SelectCountText
    {
        get => _selectCountText;
        set => SetProperty(ref _selectCountText, value);
    }


    private string? _deleteInfoText;

    public string? DeleteInfoText
    {
        get => _deleteInfoText;
        set => SetProperty(ref _deleteInfoText, value);
    }


    private bool _mutliSelect;

    public bool MutliSelect
    {
        get => _mutliSelect;
        set
        {
            if (SetProperty(ref _mutliSelect, value))
            {
                // 平台替换：Avalonia 的 SelectionMode 没有 None，非多选时用 Single + 选择后立即清空（见 SelectionChanged）。
                ListView_Images.SelectionMode = value ? SelectionMode.Multiple : SelectionMode.Single;
                UpdateSelectCountText();
            }
        }
    }



    private void LoadBackgroundItems()
    {
        try
        {
            string folder = Path.Join(AppConfig.CacheFolder, "bg");
            if (Directory.Exists(folder))
            {
                string[] files = Directory.GetFiles(folder);
                IEnumerable<BackgroundFileItem> result = files.Select(x => new BackgroundFileItem(x));
                if (RadioMenuFlyoutItem_Filter_Image.IsChecked)
                {
                    result = result.Where(x => !x.IsVideo);
                }
                if (RadioMenuFlyoutItem_Filter_Video.IsChecked)
                {
                    result = result.Where(x => x.IsVideo);
                }
                if (RadioMenuFlyoutItem_Sort_Time.IsChecked)
                {
                    result = result.OrderByDescending(x => x.CreationTime);
                }
                if (RadioMenuFlyoutItem_Sort_FileSize.IsChecked)
                {
                    result = result.OrderByDescending(x => x.FileSize);
                }
                BackgroundImages = new(result);
                StackPanel_NoFolder.IsVisible = false;
                UpdateSelectCountText();
            }
            else
            {
                StackPanel_NoFolder.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load background items.");
        }
    }



    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string folder = Path.Join(AppConfig.CacheFolder, "bg");
            if (Directory.Exists(folder))
            {
                OpenPath(folder);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open background folder.");
        }
    }


    private async void OnDeleteDuplicateClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string folder = Path.Join(AppConfig.CacheFolder, "bg");
            if (Directory.Exists(folder))
            {
                int count = 0;
                string[] files = Directory.GetFiles(folder);
                ConcurrentDictionary<string, bool> dict = new();
                await Parallel.ForEachAsync(files, async (file, _) =>
                {
                    using FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    string hash = Convert.ToHexString(await MD5.HashDataAsync(fs));
                    if (dict.TryAdd(hash, true))
                    {
                        return;
                    }
                    fs.Dispose();
                    File.Delete(file);
                    Interlocked.Increment(ref count);
                });
                DeleteInfoText = string.Format(Lang.BackgroundViewPage_0DuplicateFileSHasBeenDeleted, count);
                LoadBackgroundItems();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete duplicate background files.");
        }
    }


    private void OnFilterSortClick(object? sender, RoutedEventArgs e)
    {
        LoadBackgroundItems();
    }



    private void OnImagesDoubleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (MutliSelect)
            {
                return;
            }
            if (ListView_Images.SelectedItem is BackgroundFileItem item)
            {
                OpenPath(item.FilePath);
            }
        }
        catch { }
    }


    private void OnImagesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!MutliSelect && ListView_Images.SelectedItem is not null)
        {
            // 对齐上游 ListViewSelectionMode.None：单选态下不保留选中项（点按只用于打开/拖动）。
            ListView_Images.SelectedItem = null;
        }
        UpdateSelectCountText();
    }



    private void UpdateSelectCountText()
    {
        try
        {
            if (MutliSelect)
            {
                SelectCountText = $"{ListView_Images.SelectedItems?.Count ?? 0}/{BackgroundImages?.Count ?? 0}";
            }
            else
            {
                SelectCountText = $"{BackgroundImages?.Count ?? 0}";
            }
        }
        catch { }
    }



    private void OnItemOpenClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Control { DataContext: BackgroundFileItem item })
            {
                OpenPath(item.FilePath);
            }
        }
        catch { }
    }


    private async void OnItemCopyClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var paths = new List<string>();
            if (MutliSelect && (ListView_Images.SelectedItems?.Count ?? 0) > 0)
            {
                foreach (BackgroundFileItem item in ListView_Images.SelectedItems!.Cast<BackgroundFileItem>())
                {
                    if (File.Exists(item.FilePath))
                    {
                        paths.Add(item.FilePath);
                    }
                }
            }
            else if (sender is Control { DataContext: BackgroundFileItem item } && File.Exists(item.FilePath))
            {
                paths.Add(item.FilePath);
            }
            if (paths.Count > 0 && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                ClipboardHelper.SetText(string.Join(Environment.NewLine, paths));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy file to clipboard");
        }
    }


    private void OnItemDeleteClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (MutliSelect && (ListView_Images.SelectedItems?.Count ?? 0) > 0)
            {
                foreach (BackgroundFileItem item in ListView_Images.SelectedItems!.Cast<BackgroundFileItem>().ToList())
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                    }
                    BackgroundImages?.Remove(item);
                }
            }
            else if (sender is Control { DataContext: BackgroundFileItem item })
            {
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }
                BackgroundImages?.Remove(item);
            }
            UpdateSelectCountText();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Failed to delete image file");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete image file");
        }
    }



    /// <summary>Linux 等价 <c>Launcher.LaunchUriAsync(new(filePath))</c> / <c>LaunchFolderPathAsync</c>。</summary>
    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }


}
