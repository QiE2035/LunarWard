using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Localization;
using LunarWard.ViewModels.Starward;
using LunarWard.Views.Starward.Controls;

namespace LunarWard.Views.Starward;

/// <summary>
/// 背景图库浮层（Starward <c>Features/Background/BackgroundViewWindow</c> + <c>BackgroundViewPage</c> 的等价物）。
/// </summary>
/// <remarks>
/// 上游是独立窗口，Linux 这里沿用窗口内浮层（与其它对话框同一套 <see cref="StarwardContentDialog"/> 容器，
/// 这样 ESC / 右上角 X / 「关闭」行为一致，也不需要多窗口的 KWin 边框）。
/// 缩略图按需异步加载：官方背景是远程 URL，本地背景是文件路径，都用
/// <c>StarwardAssets.BitmapAsync</c>（同一个磁盘缓存）。
/// </remarks>
public sealed partial class BackgroundViewDialog : StarwardContentDialog
{
    private static AppStrings Strings => AppStrings.Instance;

    private readonly Action<int> _select;
    private readonly Action _openFolder;


    /// <summary>设计器/XAML 预览用。</summary>
    public BackgroundViewDialog()
        : this([], _ => Task.FromResult<Bitmap?>(null), _ => { }, () => { }, "")
    {
    }


    public BackgroundViewDialog(
        IReadOnlyList<BackgroundSwitchItem> entries,
        Func<string, Task<Bitmap?>> loader,
        Action<int> select,
        Action openFolder,
        string title,
        int currentIndex = -1)
    {
        InitializeComponent();

        _select = select;
        _openFolder = openFolder;

        Title = title;
        OpenFolderButton.Content = Strings["Launcher.GalleryOpenFolder"];
        HintText.Text = Strings["Launcher.GalleryHint"];
        CloseButtonText = Strings["Common.Close"];
        CountText.Text = string.Format(Strings["Launcher.GalleryCount"], entries.Count);

        var items = new ObservableCollection<BackgroundGalleryItem>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            items.Add(new BackgroundGalleryItem
            {
                Index = i,
                Title = string.IsNullOrWhiteSpace(entry.Title) ? entry.Value : entry.Title,
                IsVideo = entry.IsVideo,
                IsCurrent = i == currentIndex,
            });
        }

        Tiles.ItemsSource = items;
        _ = LoadThumbnailsAsync(items, entries, loader);
    }


    private static async Task LoadThumbnailsAsync(
        ObservableCollection<BackgroundGalleryItem> items,
        IReadOnlyList<BackgroundSwitchItem> entries,
        Func<string, Task<Bitmap?>> loader)
    {
        foreach (var item in items)
        {
            var entry = entries[item.Index];
            var source = entry.ImageSource ?? (entry.IsVideo ? null : entry.Value);
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            try
            {
                item.Thumbnail = await loader(source).ConfigureAwait(true);
            }
            catch
            {
                // 缩略图失败不影响选择：留空占位。
            }
        }
    }


    private void OnTileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index })
        {
            _select(index);
        }

        Close();
    }


    private void OnOpenFolderClick(object? sender, RoutedEventArgs e) => _openFolder();
}
