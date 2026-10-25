using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using System.IO;
using LunarWard.App.Localization;
using LunarWard.App.ViewModels.Starward;
using LunarWard.Launch;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// 图片查看器，复刻 Starward <c>Features/Screenshot/ImageViewWindow2</c>：
/// 顶栏命令、缩放/拖动的图片区、左右翻页、图库条、底栏缩放控制、右侧信息面板。
/// </summary>
/// <remarks>
/// <para>
/// 上游用 <c>CanvasSwapChainPanel</c> + Win2D 画图（HDR/SDR 转换、显示器亮度），
/// 这部分在 Linux 没有等价物：这里用 Avalonia <see cref="Image"/> +
/// <see cref="ScaleTransform"/>/<see cref="TranslateTransform"/> 实现缩放与拖动，
/// 编辑面板只保留「图像信息 / 显示器信息 / 导出」，HDR 相关控件如实标注不支持。
/// </para>
/// <para>
/// 指针相关的交互（滚轮锚点缩放、按下拖动、双击还原）必须留在 code-behind，
/// 与上游分工一致；可绑定的状态在 <see cref="ImageViewWindow2ViewModel"/>。
/// </para>
/// </remarks>
public partial class ImageViewWindow2 : Window
{

    private static AppStrings Strings => AppStrings.Instance;

    private readonly ImageViewWindow2ViewModel _viewModel;

    /// <summary>图片的缩放/平移（XAML 里声明 x:Name 的转换器不生成字段，这里在代码里建）。</summary>
    private readonly ScaleTransform _imageScale = new();

    private readonly TranslateTransform _imageTranslate = new();

    private Point _dragOrigin;
    private Point _dragStartOffset;
    private bool _dragging;
    /// <summary>是否处于「适应窗口」模式：用户一旦自己缩放就退出，视口变化时自动重算。</summary>
    private bool _isFitMode = true;


    public ImageViewWindow2(GameScreenshot item, IReadOnlyList<GameScreenshot> screenshots)
        : this(screenshots, Math.Max(0, screenshots.ToList().FindIndex(x => x.Path == item.Path)))
    {
    }


    public ImageViewWindow2(IReadOnlyList<GameScreenshot> screenshots, int index)
    {
        InitializeComponent();
        ImageTransformHost.LayoutTransform = _imageScale;
        ImageTransformHost.RenderTransform = _imageTranslate;
        _viewModel = new ImageViewWindow2ViewModel(screenshots, index);
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ImageLoaded += OnImageLoaded;

        Opened += (_, _) =>
        {
            SyncGallerySelection();
            FitToWindow();
        };

        KeyDown += OnWindowKeyDown;

        // Starward Grid_SizeChanged：视口尺寸变了要重新「适应窗口」（展开信息面板也算）。
        ImagePanel.SizeChanged += (_, _) =>
        {
            if (_isFitMode)
            {
                FitToWindow();
            }
        };
    }


    /// <summary>Starward <c>InvalidateImage</c>：缩放系数变化后同步到视觉树。</summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ImageViewWindow2ViewModel.ZoomFactor):
                ApplyZoom();
                break;
            case nameof(ImageViewWindow2ViewModel.IsGalleryOpen):
                if (_viewModel.IsGalleryOpen)
                {
                    SyncGallerySelection();
                }
                break;
        }
    }


    private void ApplyZoom()
    {
        var zoom = ImageViewWindow2State.ClampZoom(_viewModel.ZoomFactor);
        _imageScale.ScaleX = zoom;
        _imageScale.ScaleY = zoom;
        if (_viewModel.ZoomFactor != zoom)
        {
            _viewModel.ZoomFactor = zoom;
        }
    }


    private void OnImageLoaded()
    {
        if (_isFitMode)
        {
            FitToWindow();
        }
    }


    /// <summary>Starward <c>Button_ZoomToFitFactor_Click</c>：整图放进视口并居中。</summary>
    private void FitToWindow()
    {
        if (_viewModel.Image is not { } image || ImagePanel.Bounds.Width <= 1)
        {
            return;
        }

        double availableWidth = Math.Max(1, ImagePanel.Bounds.Width);
        double availableHeight = Math.Max(1, ImagePanel.Bounds.Height);
        double scale = Math.Min(availableWidth / image.PixelSize.Width, availableHeight / image.PixelSize.Height);
        _viewModel.ZoomFactor = ImageViewWindow2State.ClampZoom(scale);
        _imageTranslate.X = 0;
        _imageTranslate.Y = 0;
        _isFitMode = true;
    }


    /// <summary>Starward <c>ScrollViewer_Image_PointerWheelChanged</c>：以指针为锚点缩放。</summary>
    private void OnImagePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel.Image is null)
        {
            return;
        }

        double oldZoom = ImageViewWindow2State.ClampZoom(_viewModel.ZoomFactor);
        double newZoom = ImageViewWindow2State.ClampZoom(
            e.Delta.Y > 0 ? oldZoom * 1.1 : oldZoom / 1.1);
        if (Math.Abs(newZoom - oldZoom) < double.Epsilon)
        {
            return;
        }

        _isFitMode = false;
        var position = e.GetPosition(ImagePanel);
        _imageTranslate.X = position.X - ((position.X - _imageTranslate.X) * (newZoom / oldZoom));
        _imageTranslate.Y = position.Y - ((position.Y - _imageTranslate.Y) * (newZoom / oldZoom));
        _viewModel.ZoomFactor = newZoom;
        e.Handled = true;
    }


    private void OnImagePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel.Image is null || !e.GetCurrentPoint(ImagePanel).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragging = true;
        _dragOrigin = e.GetPosition(ImagePanel);
        _dragStartOffset = new Point(_imageTranslate.X, _imageTranslate.Y);
        e.Pointer.Capture(ImagePanel);
    }


    private void OnImagePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        var position = e.GetPosition(ImagePanel);
        _imageTranslate.X = _dragStartOffset.X + (position.X - _dragOrigin.X);
        _imageTranslate.Y = _dragStartOffset.Y + (position.Y - _dragOrigin.Y);
    }


    private void OnImagePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
        e.Pointer.Capture(null);
    }


    /// <summary>Starward <c>ScrollViewer_Image_DoubleTapped</c>：双击回到「适应窗口」。</summary>
    private void OnImageDoubleTapped(object? sender, TappedEventArgs e)
    {
        FitToWindow();
        e.Handled = true;
    }


    // ── 顶栏命令 ────────────────────────────────────────────────────────

    private void OnEditImageClick(object? sender, RoutedEventArgs e)
        => _viewModel.IsInfoPanelOpen = !_viewModel.IsInfoPanelOpen;


    private void OnCloseInfoClick(object? sender, RoutedEventArgs e)
        => _viewModel.IsInfoPanelOpen = false;


    /// <summary>Starward <c>MenuFlyout_Open</c>：打开新文件 / 在文件管理器中打开 / 用默认应用打开。</summary>
    private void OnOpenMenuClick(object? sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();
        flyout.Items.Add(CreateMenuItem("ImageView2.OpenNewFiles", "\uE8E5", async () => await OpenNewFilesAsync()));
        flyout.Items.Add(CreateMenuItem("ImageView2.OpenInFileManager", "\uEC50", OpenInFileManager));
        flyout.Items.Add(CreateMenuItem("ImageView2.OpenWithDefaultApp", "\uE91B", OpenInDefaultApp));
        flyout.ShowAt(ButtonOpenMenu);
    }


    private static MenuItem CreateMenuItem(string key, string glyph, Action action)
    {
        var item = new MenuItem
        {
            Header = Strings[key],
            Icon = new TextBlock { Classes = { "icon" }, Text = glyph },
        };
        item.Click += (_, _) => action();
        return item;
    }


    /// <summary>Starward <c>MenuFlyoutItem_OpenNewFile_Click</c>：把选中的图片追加进集合。</summary>
    private async Task OpenNewFilesAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings["ImageView2.OpenNewFiles"],
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"],
                },
            ],
        });
        if (files.Count == 0)
        {
            return;
        }

        var startIndex = _viewModel.Items.Count;
        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is not { Length: > 0 } path || !File.Exists(path))
            {
                continue;
            }
            _viewModel.Items.Add(new ImageViewItem(new GameScreenshot(path, new DateTimeOffset(File.GetLastWriteTime(path)), new FileInfo(path).Length)));
        }
        if (_viewModel.Items.Count > startIndex)
        {
            _viewModel.Select(startIndex);
            SyncGallerySelection();
        }
    }


    private async void OnOpenNewFilesClick(object? sender, RoutedEventArgs e)
        => await OpenNewFilesAsync();


    private void OnCopyPathClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedItem is not { } item)
        {
            return;
        }
        _ = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(item.Path);
    }


    /// <summary>Starward <c>Button_DeleteImage_Click</c>：删文件并从集合里移除。</summary>
    private void OnDeleteConfirmedClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedItem is not { } item)
        {
            return;
        }
        try
        {
            File.Delete(item.Path);
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"[ImageView2] 删除失败：{ex}");
#endif
            _ = ex; // Release 下 DEBUG 段不参与编译，显式消费避免 CS0168
        }

        var index = _viewModel.SelectedIndex;
        _viewModel.Items.Remove(item);
        if (_viewModel.Items.Count == 0)
        {
            Close();
            return;
        }
        _viewModel.Select(Math.Clamp(index, 0, _viewModel.Items.Count - 1));
        _viewModel.RefreshDerived();
        SyncGallerySelection();
    }


    private void OnFullScreenClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.IsFullScreen = !_viewModel.IsFullScreen;
        WindowState = _viewModel.IsFullScreen ? WindowState.FullScreen : WindowState.Normal;
    }


    // ── 底栏命令 ────────────────────────────────────────────────────────

    private void OnFitClick(object? sender, RoutedEventArgs e) => FitToWindow();


    /// <summary>拖动缩放滑块算「手动缩放」，此后视口变化不再自动适应（与上游一致）。</summary>
    private void OnZoomSliderPointerPressed(object? sender, PointerPressedEventArgs e) => _isFitMode = false;


    private void OnZoomInClick(object? sender, RoutedEventArgs e)
    {
        _isFitMode = false;
        _viewModel.ZoomFactor = ImageViewWindow2State.ZoomIn(_viewModel.ZoomFactor);
        CenterImage();
    }


    private void OnZoomOutClick(object? sender, RoutedEventArgs e)
    {
        _isFitMode = false;
        _viewModel.ZoomFactor = ImageViewWindow2State.ZoomOut(_viewModel.ZoomFactor);
        CenterImage();
    }


    /// <summary>
    /// 让缩放后的图仍然居中。
    /// </summary>
    /// <remarks>
    /// 图片按 Stretch=None 布局，未缩放时由居中接管，其左上角在 <c>(panel - image) / 2</c>；
    /// RenderTransform 以图片左上角为原点缩放，所以再补一半的「缩放差」即可回正。
    /// </remarks>
    private void CenterImage()
    {
        if (_viewModel.Image is not { } image)
        {
            return;
        }
        _imageTranslate.X = 0;
        _imageTranslate.Y = 0;
    }


    private void OnPreviousClick(object? sender, RoutedEventArgs e) => NavigateTo(_viewModel.SelectedIndex - 1);


    private void OnNextClick(object? sender, RoutedEventArgs e) => NavigateTo(_viewModel.SelectedIndex + 1);


    private void NavigateTo(int index)
    {
        if (index < 0 || index >= _viewModel.Items.Count)
        {
            return;
        }
        _viewModel.Select(index);
        SyncGallerySelection();
        _isFitMode = true;
        FitToWindow();
    }


    /// <summary>Starward <c>RootGrid_KeyDown</c>：← → 翻页、Esc 关闭、Ctrl+0 适应窗口。</summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                NavigateTo(_viewModel.SelectedIndex - 1);
                e.Handled = true;
                break;
            case Key.Right:
                NavigateTo(_viewModel.SelectedIndex + 1);
                e.Handled = true;
                break;
            case Key.Escape when _viewModel.IsFullScreen:
                OnFullScreenClick(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.D0 when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                FitToWindow();
                e.Handled = true;
                break;
        }
    }


    // ── 图库 ────────────────────────────────────────────────────────────

    private void SyncGallerySelection()
    {
        foreach (var item in _viewModel.Items)
        {
            item.EnsureThumbnail();
        }
        if (GalleryList.ItemCount > _viewModel.SelectedIndex && _viewModel.SelectedIndex >= 0)
        {
            GalleryList.SelectedIndex = _viewModel.SelectedIndex;
        }
    }


    private void OnGallerySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (GalleryList.SelectedIndex >= 0 && GalleryList.SelectedIndex != _viewModel.SelectedIndex)
        {
            _viewModel.Select(GalleryList.SelectedIndex);
            _isFitMode = true;
            FitToWindow();
        }
    }


    // ── 外部命令 ────────────────────────────────────────────────────────

    private void OpenInFileManager()
    {
        if (_viewModel.SelectedItem is not { } item)
        {
            return;
        }
        TryStart("xdg-open", item.Folder);
    }


    private void OpenInDefaultApp()
    {
        if (_viewModel.SelectedItem is not { } item)
        {
            return;
        }
        TryStart("xdg-open", item.Path);
    }


    private static void TryStart(string fileName, string argument)
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName, argument) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"[ImageView2] 打开失败：{ex}");
#endif
            _ = ex; // Release 下 DEBUG 段不参与编译，显式消费避免 CS0168
        }
    }


    /// <summary>Starward 的「导出图像」：另存为新的文件（HDR 转换在 Linux 不做）。</summary>
    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedItem is not { } item)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings["ImageView2.ExportImage"],
            SuggestedFileName = item.FileName,
            DefaultExtension = Path.GetExtension(item.Path).TrimStart('.'),
        });
        if (file?.TryGetLocalPath() is not { Length: > 0 } target)
        {
            return;
        }

        try
        {
            File.Copy(item.Path, target, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EXPORT_FAIL {ex.GetType().Name}: {ex.Message}");
        }
    }


    // ── 自检截图 ────────────────────────────────────────────────────────

    /// <summary>自检时预先展开图库/信息面板（真机上由按钮切换）。</summary>
    public void PrepareCapture(bool gallery, bool info)
    {
        _viewModel.IsGalleryOpen = gallery;
        _viewModel.IsInfoPanelOpen = info;
        _isFitMode = true;
        Dispatcher.UIThread.Post(FitToWindow, DispatcherPriority.Loaded);
    }


    /// <summary>
    /// 画三张 1920×1080 渐变样例图，只给自检截图用（真机上这些来自游戏截图目录）。
    /// </summary>
    public static IReadOnlyList<GameScreenshot> CreateCaptureSamples()
    {
        string directory = System.IO.Path.Join(System.IO.Path.GetTempPath(), "lw-viewer-samples");
        System.IO.Directory.CreateDirectory(directory);

        var colors = new (Color From, Color To, string Label)[]
        {
            (Color.Parse("#FF3B4D7A"), Color.Parse("#FFB04BC6"), "Night Sky"),
            (Color.Parse("#FF1F6F5A"), Color.Parse("#FFE0C060"), "Forest"),
            (Color.Parse("#FF7A2B3B"), Color.Parse("#FF3B6E9E"), "Harbor"),
        };

        var list = new List<GameScreenshot>();
        for (int i = 0; i < colors.Length; i++)
        {
            string file = System.IO.Path.Join(directory, $"sample-{i + 1}.png");
            var size = new PixelSize(1920, 1080);
            using var target = new RenderTargetBitmap(size, new Vector(96, 96));
            using (var context = target.CreateDrawingContext())
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(colors[i].From, 0),
                        new GradientStop(colors[i].To, 1),
                    },
                };
                context.FillRectangle(brush, new Rect(0, 0, 1920, 1080));
                var text = new FormattedText(
                    colors[i].Label,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter"),
                    140,
                    Brushes.White);
                context.DrawText(text, new Point(140, 140));
            }
            target.Save(file, new PngBitmapEncoderOptions());
            list.Add(new GameScreenshot(
                file,
                DateTimeOffset.Now.AddMinutes(-i * 17),
                new FileInfo(file).Length));
        }
        return list;
    }

    /// <summary>把本窗口渲染成 PNG（与 InstallGameDialog.CaptureSelf 同构）。</summary>
    public void CaptureSelf(string path, int delayMs)
    {
        Avalonia.Threading.DispatcherTimer.RunOnce(() =>
        {
            try
            {
                var size = new PixelSize(
                    Math.Max(1, (int)Bounds.Width),
                    Math.Max(1, (int)Bounds.Height));
                using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
                bitmap.Render(this);
                using (var stream = File.Create(path))
                {
                    bitmap.Save(stream, new PngBitmapEncoderOptions());
                }
                Console.WriteLine($"SCREENSHOT_OK {path} {size.Width}x{size.Height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SCREENSHOT_FAIL {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Close();
                if (Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(0);
                }
            }
        }, TimeSpan.FromMilliseconds(delayMs));
    }

}
