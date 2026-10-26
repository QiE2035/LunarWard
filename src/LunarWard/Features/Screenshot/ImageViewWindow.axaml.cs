using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Helpers;
using LunarWard.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ImageViewWindow</c>（XAML 193 行 + code-behind 565 行）的 Avalonia 移植。
/// </summary>
/// <remarks>
/// <para>
/// 保留的上游行为：打开即最大化、滚轮切换同集合图片（按住 Ctrl 则缩放）、
/// 单击显示/隐藏工具栏与底部缩略图条、双击「放大 → 100% → 适应窗口」循环、
/// 拖拽平移、旋转 90°、复制图片（图标 3 秒后复原）、用默认应用打开、
/// Esc 关闭、F11 全屏。
/// </para>
/// <para>
/// 未移植（Windows 专有或依赖未落地模块，已登记 docs/09）：
/// <c>WindowEx</c> 标题栏按钮与拖动区域、<c>ScrollViewer.ZoomMode</c> 内置缩放、
/// <c>CachedImage.Rotation</c>、亚克力背景与 <c>ThemeShadow</c>、
/// <c>GWX</c> 的 <c>UIScale</c>（这里按 1.0 计算缩放率文本）。
/// 缩放/平移改为 RenderTransform + 自维护状态（与 <see cref="ImageViewWindow2"/> 一致）。
/// </para>
/// </remarks>
public partial class ImageViewWindow : Window
{


    private readonly ILogger<ImageViewWindow> _logger = AppConfig.GetLogger<ImageViewWindow>();

    private double _scale = 1;

    private bool _canImageMoved;

    private Point _oldPosition;


    public ImageViewWindow()
    {
        InitializeComponent();
        DataContext = this;
        InitializeWindow();
        Closed += ImageViewWindow_Closed;
        KeyDown += RootGrid_KeyDown;
    }



    private void InitializeWindow()
    {
        Title = "Starward Screenshots Viewer";
        WindowState = WindowState.Maximized;
    }



    private void ImageViewWindow_Closed(object? sender, EventArgs args)
    {
        // 上游注释同样承认 ImageViewWindow 有内存泄漏；这里至少断开引用与事件。
        KeyDown -= RootGrid_KeyDown;
        _Image.Source = null;
        CurrentImage = null!;
        ImageCollection = null;
    }



    private void RootGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Handled)
            {
                return;
            }
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            if (e.Key == Key.F11)
            {
                FullScreen();
                e.Handled = true;
            }
        }
        catch { }
    }



    public bool DecodeFromStream { get; set; }


    public bool EnableLoadingRing { get; set; }


    public ScreenshotItem CurrentImage
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                LoadCurrentImage();
            }
        }
    } = null!;



    public List<ScreenshotItem>? ImageCollection
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                bool any = value?.Any() ?? false;
                _Border_ImageCollection.IsVisible = any;
            }
        }
    }



    /// <summary>
    /// 把 <see cref="CurrentImage"/> 解码到 <see cref="_Image"/>，并按窗口计算适应缩放率。
    /// </summary>
    private void LoadCurrentImage()
    {
        RotateTransform_Image.Angle = 0;
        TranslateTransform_Image.X = 0;
        TranslateTransform_Image.Y = 0;
        _scale = 1;
        ScaleTransform_Image.ScaleX = 1;
        ScaleTransform_Image.ScaleY = 1;
        Bitmap? bitmap = null;
        if (!string.IsNullOrWhiteSpace(CurrentImage?.FilePath))
        {
            try
            {
                // 上游 ImageEx 从 URI 解码（支持 ms-appx/file）；本地截图都是普通文件路径。
                bitmap = new Bitmap(CurrentImage.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load image: {file}", CurrentImage.FilePath);
            }
        }
        _Image.Source = bitmap;
        _Image.Width = bitmap?.PixelSize.Width ?? double.NaN;
        _Image.Height = bitmap?.PixelSize.Height ?? double.NaN;
        if (bitmap is not null)
        {
            Dispatcher.UIThread.Post(FitToWindow, DispatcherPriority.Loaded);
        }
    }



    /// <summary>
    /// 缩放
    /// </summary>
    /// <param name="factor">缩放因子</param>
    private void Zoom(double factor)
    {
        var newFactor = Math.Clamp(factor, 0.1, 2);
        var oldFactor = _scale;
        if (Math.Abs(oldFactor) < double.Epsilon)
        {
            oldFactor = 1;
        }
        // 保证图片以画面中心为基点进行缩放（对应上游 ScrollViewer 的 offset 换算）。
        var center = new Point(Grid_ImageHost.Bounds.Width / 2, Grid_ImageHost.Bounds.Height / 2);
        var ratio = newFactor / oldFactor;
        TranslateTransform_Image.X = center.X - ((center.X - TranslateTransform_Image.X) * ratio);
        TranslateTransform_Image.Y = center.Y - ((center.Y - TranslateTransform_Image.Y) * ratio);
        _scale = newFactor;
        ScaleTransform_Image.ScaleX = newFactor;
        ScaleTransform_Image.ScaleY = newFactor;
        UpdateFactorText();
    }



    private void UpdateFactorText()
    {
        // 上游乘 GWX UIScale（Windows 每显示器 DPI）；Avalonia 里缩放率文本直接用比例。
        _TextBlock_Factor.Text = _scale.ToString("P0");
    }



    /// <summary>
    /// 缩小
    /// </summary>
    [RelayCommand]
    private void ZoomOut()
    {
        // 缩放率调整为 0.1 的倍数
        var factor = Math.Ceiling(_scale * 10 - 1.1) / 10;
        Zoom(factor);
    }



    /// <summary>
    /// 放大
    /// </summary>
    [RelayCommand]
    private void ZoomIn()
    {
        var factor = Math.Floor(_scale * 10 + 1.1) / 10;
        Zoom(factor);
    }



    /// <summary>
    /// 顺时针旋转90°
    /// </summary>
    [RelayCommand]
    private void Rotate()
    {
        RotateTransform_Image.Angle += 90;
        FitToWindow();
    }



    /// <summary>
    /// 复制图片
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task CopyImageAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentImage.FilePath) || !File.Exists(CurrentImage.FilePath))
            {
                _logger.LogWarning("Cannot find file: {file}", CurrentImage.FilePath);
                return;
            }
            var storageFile = await StorageProvider.TryGetFileFromPathAsync(CurrentImage.FilePath);
            if (storageFile is null)
            {
                _logger.LogWarning("Cannot find file: {file}", CurrentImage.FilePath);
                return;
            }
            // 上游 ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, file)
            if (Clipboard is not null)
            {
                await Clipboard.SetFilesAsync([storageFile]);
            }
            if (Button_Copy.Content is TextBlock icon)
            {
                icon.Text = "\xE8FB";
                await Task.Delay(3000);
                icon.Text = "\xE8C8";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy image");
        }
    }



    /// <summary>
    /// 打开图片
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenFileAsync()
    {
        try
        {
            if (File.Exists(CurrentImage.FilePath))
            {
                // 上游 Launcher.LaunchUriAsync(new Uri(file))：Linux 用 xdg-open。
                Process.Start(new ProcessStartInfo { FileName = "xdg-open", ArgumentList = { CurrentImage.FilePath }, UseShellExecute = false });
            }
            else
            {
                _logger.LogWarning("Cannot find file: {file}", CurrentImage.FilePath);
            }
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open file");
        }
    }



    /// <summary>
    /// 关闭页面
    /// </summary>
    [RelayCommand]
    private new void Close()
    {
        base.Close();
    }



    /// <summary>
    /// 图片加载后计算合适的缩放率
    /// </summary>
    private void FitToWindow()
    {
        if (_Image.Source is not Bitmap bitmap)
        {
            return;
        }
        var factor = GetFitZoomFactor();
        _scale = factor;
        ScaleTransform_Image.ScaleX = factor;
        ScaleTransform_Image.ScaleY = factor;
        TranslateTransform_Image.X = 0;
        TranslateTransform_Image.Y = 0;
        UpdateFactorText();
    }



    /// <summary>
    /// 计算适合窗口的图片缩放率
    /// </summary>
    /// <returns></returns>
    private double GetFitZoomFactor()
    {
        double imageWidth = _Image.Width;
        double imageHeight = _Image.Height;
        if (double.IsNaN(imageWidth) || double.IsNaN(imageHeight) || imageWidth * imageHeight == 0)
        {
            return 1;
        }
        double viewportWidth = Grid_ImageHost.Bounds.Width;
        double viewportHeight = Grid_ImageHost.Bounds.Height;
        if (viewportWidth * viewportHeight == 0)
        {
            return 1;
        }
        double factor;
        if ((int)RotateTransform_Image.Angle % 180 == 0)
        {
            var widthFactor = viewportWidth / imageWidth;
            var heightFactor = viewportHeight / imageHeight;
            factor = Math.Min(widthFactor, heightFactor);
        }
        else
        {
            var widthFactor = viewportHeight / imageWidth;
            var heightFactor = viewportWidth / imageHeight;
            factor = Math.Min(widthFactor, heightFactor);
        }
        return Math.Min(factor, 1);
    }



    /// <summary>
    /// 鼠标单击，显示或隐藏图片缩率图栏
    /// </summary>
    private void _ScrollViewer_Image_Tapped(object? sender, TappedEventArgs e)
    {
        ChangeToolBarVisibility();
    }



    /// <summary>
    /// 更改工具栏的可见性
    /// </summary>
    private void ChangeToolBarVisibility()
    {
        if (_Border_ToolBar.IsHitTestVisible)
        {
            _Border_ToolBar.Opacity = 0;
            _Border_ToolBar.IsHitTestVisible = false;
            if (ImageCollection?.Count > 0)
            {
                _Border_ImageCollection.Opacity = 0;
                _Border_ImageCollection.IsHitTestVisible = false;
            }
        }
        else
        {
            _Border_ToolBar.Opacity = 1;
            _Border_ToolBar.IsHitTestVisible = true;
            if (ImageCollection?.Count > 0)
            {
                _Border_ImageCollection.Opacity = 1;
                _Border_ImageCollection.IsHitTestVisible = true;
            }
        }
    }



    /// <summary>
    /// 双击视觉元素时的处理（上游 _ScrollViewer_Image_DoubleTapped）
    /// </summary>
    private void _ScrollViewer_Image_DoubleTapped(object? sender, TappedEventArgs e)
    {
        ChangeToolBarVisibility();
        var oldFactor = _scale;
        double newFactor;
        if (oldFactor < 0.4)
        {
            newFactor = oldFactor * 2;
        }
        else if (oldFactor < 0.999999)
        {
            newFactor = 1;
        }
        else
        {
            newFactor = GetFitZoomFactor();
        }
        Zoom(newFactor);
    }



    /// <summary>
    /// 鼠标滚动，切换图片（按住 Ctrl 时缩放）
    /// </summary>
    private void _ScrollViewer_Image_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (ctrl)
        {
            // 上游此处不处理 Ctrl 缩放（交给 ScrollViewer.ZoomMode）；Avalonia 无内置缩放，这里补上。
            Zoom(e.Delta.Y > 0 ? _scale * 1.1 : _scale / 1.1);
            e.Handled = true;
            return;
        }
        if (ImageCollection is { Count: > 0 } collection && CurrentImage is not null)
        {
            var index = collection.IndexOf(CurrentImage);
            if (e.Delta.Y < 0)
            {
                if (index < collection.Count - 1)
                {
                    CurrentImage = collection[index + 1];
                }
            }
            else
            {
                if (index > 0)
                {
                    CurrentImage = collection[index - 1];
                }
            }
        }
        // 阻止事件传递到滚动条
        e.Handled = true;
    }



    private void _ScrollViewer_Image_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _canImageMoved = true;
        _oldPosition = e.GetPosition(Grid_ImageHost);
    }



    /// <summary>
    /// 鼠标拖动图片
    /// </summary>
    private void _ScrollViewer_Image_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_canImageMoved)
        {
            var pointer = e.GetCurrentPoint(Grid_ImageHost);
            if (pointer.Properties.IsLeftButtonPressed)
            {
                var position = pointer.Position;
                var deltaX = position.X - _oldPosition.X;
                var deltaY = position.Y - _oldPosition.Y;
                _oldPosition = position;
                // offset 的方向应与鼠标移动的方向相反
                TranslateTransform_Image.X += deltaX;
                TranslateTransform_Image.Y += deltaY;
            }
        }
    }



    private void _ScrollViewer_Image_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _canImageMoved = false;
    }



    /// <summary>
    /// 底部缩略图条点击切换当前图片（上游 GridView.SelectedItem 双向绑定）
    /// </summary>
    private void ImageCollectionItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ScreenshotItem item })
        {
            CurrentImage = item;
        }
    }



    [RelayCommand]
    private void FullScreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
            if (Button_FullScreen.Content is TextBlock icon)
            {
                icon.Text = "\xE740";
            }
        }
        else
        {
            WindowState = WindowState.FullScreen;
            if (Button_FullScreen.Content is TextBlock icon)
            {
                icon.Text = "\xE73F";
            }
        }
    }


}
