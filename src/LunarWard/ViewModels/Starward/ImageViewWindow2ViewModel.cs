using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Localization;
using LunarWard.Launch;
using System.IO;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 图片查看器窗口的状态（复刻 Starward <c>Features/Screenshot/ImageViewWindow2</c>）。
/// </summary>
/// <remarks>
/// 上游把 1572 行逻辑几乎全放在 code-behind（滚动、指针、CanvasSwapChain 画图）。
/// 这里把「可绑定的状态」收进 VM，把真正要窗口/指针参与的交互（缩放锚点、拖动、
/// 全屏切换、文件对话框）留在 <c>ImageViewWindow2</c> 的 code-behind，与上游分工一致。
/// </remarks>
public sealed partial class ImageViewWindow2ViewModel : ObservableObject
{

    private static AppStrings Strings => AppStrings.Instance;

    private int _loadVersion;


    public ImageViewWindow2ViewModel(IReadOnlyList<GameScreenshot> screenshots, int index)
    {
        Items = new ObservableCollection<ImageViewItem>(screenshots.Select(x => new ImageViewItem(x)));
        _selectedIndex = Math.Clamp(index, 0, Math.Max(0, Items.Count - 1));
        LoadCurrentImage();
    }


    /// <summary>窗口里所有的图（底部图库与上一张/下一张都吃它）。</summary>
    public ObservableCollection<ImageViewItem> Items { get; }


    public bool HasItems => Items.Count > 0;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(HasImage), nameof(HasError),
        nameof(IndexText), nameof(PixelSizeText), nameof(FileSizeText),
        nameof(ImageInformationText), nameof(MonitorInformationText))]
    private int _selectedIndex;


    public ImageViewItem? SelectedItem => Items.Count == 0 ? null : Items[Math.Clamp(SelectedIndex, 0, Items.Count - 1)];


    /// <summary>Starward <c>Title="{x:Bind CurrentFileName}"</c>。</summary>
    public string Title => SelectedItem?.FileName ?? Strings["ImageView2.NoImage"];


    [ObservableProperty]
    private Bitmap? _image;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private bool _hasError;


    [ObservableProperty]
    private string? _errorText;


    public bool HasImage => Image is not null && !HasError;


    /// <summary>Starward <c>1/12</c> 索引；没有图时不显示。</summary>
    public string IndexText => ImageViewWindow2State.IndexText(SelectedIndex, Items.Count);


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomPercentText))]
    private double _zoomFactor = 1.0;


    public string ZoomPercentText => ImageViewWindow2State.ZoomPercent(ZoomFactor);


    public string PixelSizeText => Image is { } image
        ? ImageViewWindow2State.PixelSizeText(image.PixelSize.Width, image.PixelSize.Height)
        : "";


    public string FileSizeText => SelectedItem is { } item
        ? ImageViewWindow2State.FileSizeText(item.SizeBytes)
        : "";


    public string ImageInformationText => SelectedItem is { } item
        ? $"{item.FileName}\n{ImageViewWindow2State.PixelSizeText(Image?.PixelSize.Width ?? 0, Image?.PixelSize.Height ?? 0)}\n{ImageViewWindow2State.FileSizeText(item.SizeBytes)}"
        : "";


    /// <summary>Starward 从 HDR 元数据里读显示器信息；Linux 只能给路径与时间。</summary>
    public string MonitorInformationText => SelectedItem is { } item
        ? $"{item.Folder}\n{item.CapturedAtText}"
        : "";


    [ObservableProperty]
    private bool _isGalleryOpen;


    [ObservableProperty]
    private bool _isInfoPanelOpen;


    [ObservableProperty]
    private bool _isFullScreen;


    /// <summary>Starward <c>SelectionChanged</c>：切图时重新解码大图。</summary>
    partial void OnSelectedIndexChanged(int value)
    {
        if (value >= 0 && value < Items.Count)
        {
            LoadCurrentImage();
        }
    }


    public void Select(int index)
    {
        if (Items.Count == 0)
        {
            return;
        }
        var target = Math.Clamp(index, 0, Items.Count - 1);
        if (target == SelectedIndex)
        {
            return;
        }
        SelectedIndex = target;
        if (IsGalleryOpen)
        {
            GallerySelectionChanged?.Invoke(target);
        }
    }


    /// <summary>图库列表的选中项回写（窗口自己维护 ListBox 的 SelectedItem）。</summary>
    public event Action<int>? GallerySelectionChanged;


    /// <summary>集合被增删后把索引/像素/大小等派生文案整体刷新一次。</summary>
    public void RefreshDerived()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IndexText));
        OnPropertyChanged(nameof(PixelSizeText));
        OnPropertyChanged(nameof(FileSizeText));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(ImageInformationText));
        OnPropertyChanged(nameof(MonitorInformationText));
    }


    public void Previous() => Select(SelectedIndex - 1);


    public void Next() => Select(SelectedIndex + 1);


    /// <summary>Starward 把当前图解码到 SwapChain；这里直接解到位图。</summary>
    private void LoadCurrentImage()
    {
        int version = Interlocked.Increment(ref _loadVersion);
        if (SelectedItem is not { } item)
        {
            Image = null;
            HasError = false;
            ErrorText = null;
            return;
        }

        string path = item.Path;
        _ = Task.Run(() =>
        {
            Bitmap? bitmap = null;
            Exception? error = null;
            try
            {
                using var stream = File.OpenRead(path);
                bitmap = new Bitmap(stream);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Dispatcher.UIThread.Post(() =>
            {
                // 快速连按上一张/下一张时，旧解码结果不能覆盖新选中项。
                if (version != _loadVersion)
                {
                    bitmap?.Dispose();
                    return;
                }
                Image = bitmap;
                HasError = error is not null;
                ErrorText = error is null ? null : $"{error.GetType().Name}: {error.Message}";
                OnPropertyChanged(nameof(HasImage));
                OnPropertyChanged(nameof(PixelSizeText));
                OnPropertyChanged(nameof(ImageInformationText));
                OnPropertyChanged(nameof(MonitorInformationText));
                ImageLoaded?.Invoke();
            });
        });
    }


    /// <summary>大图解码完成（窗口据此做「适应窗口」与居中）。</summary>
    public event Action? ImageLoaded;

}


/// <summary>图库里的一项（Starward <c>ScreenshotItem</c>）。</summary>
public sealed partial class ImageViewItem : ObservableObject
{

    private int _thumbnailState;


    public ImageViewItem(GameScreenshot screenshot)
    {
        Path = screenshot.Path;
        FileName = screenshot.FileName;
        SizeBytes = screenshot.SizeBytes;
        Folder = System.IO.Path.GetDirectoryName(screenshot.Path) ?? "";
        CapturedAtText = screenshot.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss");
    }


    public string Path { get; }

    public string FileName { get; }

    public long SizeBytes { get; }

    public string Folder { get; }

    public string CapturedAtText { get; }


    /// <summary>图库缩略图（Starward <c>CachedImage Width=128 Height=72 IsThumbnail</c>）。</summary>
    [ObservableProperty]
    private Bitmap? _thumbnail;


    public void EnsureThumbnail()
    {
        if (Interlocked.Exchange(ref _thumbnailState, 1) != 0)
        {
            return;
        }

        string path = Path;
        _ = Task.Run(() =>
        {
            Bitmap? bitmap = null;
            try
            {
                using var stream = File.OpenRead(path);
                bitmap = Bitmap.DecodeToWidth(stream, 256);
            }
            catch (Exception)
            {
                // 坏图：图库只显示背景色，与上游 CachedImage 一致。
            }

            if (bitmap is not null)
            {
                Dispatcher.UIThread.Post(() => Thumbnail = bitmap);
            }
        });
    }

}
