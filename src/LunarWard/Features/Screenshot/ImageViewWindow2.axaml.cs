using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LunarWard.Helpers;
using LunarWard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using LunarWard.Language;
using Avalonia.Media;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ImageViewWindow2</c>（XAML 731 行 + code-behind 1572 行）的 Avalonia 移植。
/// </summary>
/// <remarks>
/// <para>
/// 保留的上游行为：顶栏（文件名、复制图片、另存为、在文件夹中显示、删除、信息）、
/// 图片区滚轮锚点缩放 + 按住拖动 + 双击适应窗口、左右翻页（按钮与 ←/→ 键）、
/// 图库集合与索引联动（打开时定位到被点击的截图）、删除后自动跳到下一张并同步集合。
/// </para>
/// <para>
/// 未移植（Windows 专有或依赖未落地模块，已登记 docs/09）：
/// <c>CanvasSwapChainPanel</c> + Win2D 的 HDR/SDR 渲染与显示器亮度、
/// 视频/GIF 播放、HDR 亮度滑杆、显示器 ICC/色域面板、图库条（横向缩略图条）、
/// JxlSharp/ImageSharp 编码导出、裁剪与 EXIF 旋转编辑。
/// 图像本身用 Avalonia <see cref="Bitmap"/> 解码（支持 png/jpg/webp/bmp/gif 首帧）。
/// </para>
/// </remarks>
public partial class ImageViewWindow2 : Window
{

    private TransformGroup? _TransformGroup_Image => (TransformGroup?)Image_Main.RenderTransform;

    private ScaleTransform ScaleTransform_Image => (ScaleTransform)_TransformGroup_Image!.Children[0];

    private TranslateTransform TranslateTransform_Image => (TranslateTransform)_TransformGroup_Image!.Children[1];


    private readonly ObservableCollection<ScreenshotItem>? _collection;

    private int _index;

    private double _scale = 1;

    private Point _dragStart;

    private Vector _dragOrigin;

    private bool _dragging;


    /// <summary>
    /// Avalonia 要求 x:Class 窗口有公共无参构造（XAML 编译期实例化校验）；上游 WinUI 无此约束。
    /// 业务入口仍是下面两个带参构造。
    /// </summary>
    public ImageViewWindow2()
    {
        InitializeComponent();
    }


    public ImageViewWindow2(ScreenshotItem item, ObservableCollection<ScreenshotItem>? collection)
    {
        InitializeComponent();
        _collection = collection;
        _index = collection is null ? 0 : Math.Max(0, collection.IndexOf(item));
        UpdateCurrent();
    }


    public ImageViewWindow2(ObservableCollection<ScreenshotItem> collection, int index)
    {
        InitializeComponent();
        _collection = collection;
        _index = index;
        UpdateCurrent();
    }


    private ScreenshotItem? Current => _collection is { Count: > 0 } && _index >= 0 && _index < _collection.Count ? _collection[_index] : null;



    private void UpdateCurrent()
    {
        if (Current is not ScreenshotItem item)
        {
            Close();
            return;
        }
        Title = item.FileName;
        TextBlock_FileName.Text = item.FileName;
        try
        {
            // 上游把图片解码到 CanvasBitmap；这里等比例载入到 Avalonia Bitmap。
            Image_Main.Source = new Bitmap(item.FilePath);
        }
        catch
        {
            Image_Main.Source = null;
        }
        bool multiple = _collection is { Count: > 1 };
        Button_Previous.IsVisible = multiple;
        Button_Next.IsVisible = multiple;
        ResetTransform();
    }



    private void ResetTransform()
    {
        _scale = 1;
        ScaleTransform_Image.ScaleX = 1;
        ScaleTransform_Image.ScaleY = 1;
        TranslateTransform_Image.X = 0;
        TranslateTransform_Image.Y = 0;
    }



    private void ZoomAt(Point point, double delta)
    {
        double oldScale = _scale;
        _scale = Math.Clamp(_scale * (delta > 0 ? 1.1 : 1 / 1.1), 0.1, 20);
        double factor = _scale / oldScale;
        // 以鼠标位置为锚点缩放：先把锚点换算到图像坐标，再补偿平移。
        Point center = new(Grid_ImageHost.Bounds.Width / 2, Grid_ImageHost.Bounds.Height / 2);
        TranslateTransform_Image.X = point.X - center.X - ((point.X - center.X - TranslateTransform_Image.X) * factor);
        TranslateTransform_Image.Y = point.Y - center.Y - ((point.Y - center.Y - TranslateTransform_Image.Y) * factor);
        ScaleTransform_Image.ScaleX = _scale;
        ScaleTransform_Image.ScaleY = _scale;
    }



    private void Grid_ImageHost_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        ZoomAt(e.GetPosition(Grid_ImageHost), e.Delta.Y);
        e.Handled = true;
    }



    private void Grid_ImageHost_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(Grid_ImageHost).Properties.IsLeftButtonPressed)
        {
            _dragging = true;
            _dragStart = e.GetPosition(Grid_ImageHost);
            _dragOrigin = new Vector(TranslateTransform_Image.X, TranslateTransform_Image.Y);
        }
    }



    private void Grid_ImageHost_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging)
        {
            Point point = e.GetPosition(Grid_ImageHost);
            TranslateTransform_Image.X = _dragOrigin.X + (point.X - _dragStart.X);
            TranslateTransform_Image.Y = _dragOrigin.Y + (point.Y - _dragStart.Y);
        }
    }



    private void Grid_ImageHost_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
    }



    private void Grid_ImageHost_DoubleTapped(object? sender, TappedEventArgs e)
    {
        ResetTransform();
    }



    private void Button_Previous_Click(object? sender, RoutedEventArgs e) => Move(-1);

    private void Button_Next_Click(object? sender, RoutedEventArgs e) => Move(1);



    private void Move(int delta)
    {
        if (_collection is { Count: > 0 })
        {
            _index = (_index + delta + _collection.Count) % _collection.Count;
            UpdateCurrent();
        }
    }



    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                Move(-1);
                e.Handled = true;
                break;
            case Key.Right:
                Move(1);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.OemPlus or Key.Add:
                ZoomAt(new Point(Grid_ImageHost.Bounds.Width / 2, Grid_ImageHost.Bounds.Height / 2), 1);
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract:
                ZoomAt(new Point(Grid_ImageHost.Bounds.Width / 2, Grid_ImageHost.Bounds.Height / 2), -1);
                e.Handled = true;
                break;
        }
    }



    private async void Button_Copy_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Current is { } item && TopLevel.GetTopLevel(this) is { } topLevel)
            {
                var file = await topLevel.StorageProvider.TryGetFileFromPathAsync(item.FilePath);
                if (file is not null)
                {
                    await topLevel.Clipboard!.SetFilesAsync([file]);
                    InAppToast.MainWindow?.Success(Lang.ImageViewWindow2_CopiedToClipboard, item.FileName);
                }
            }
        }
        catch (Exception ex)
        {
            InAppToast.MainWindow?.Error(ex);
        }
    }



    private async void Button_SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Current is { } item && TopLevel.GetTopLevel(this) is { } topLevel)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save image",
                    SuggestedFileName = item.FileName,
                });
                if (file is not null)
                {
                    string target = file.Path.LocalPath;
                    File.Copy(item.FilePath, target, overwrite: true);
                }
            }
        }
        catch (Exception ex)
        {
            InAppToast.MainWindow?.Error(ex);
        }
    }



    private void Button_OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Current is { } item)
            {
                using Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { Path.GetDirectoryName(item.FilePath)! },
                    UseShellExecute = false,
                })!;
            }
        }
        catch { }
    }



    private void Button_Delete_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Current is { } item)
            {
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }
                _collection?.Remove(item);
                if (_collection is { Count: > 0 })
                {
                    _index = Math.Min(_index, _collection.Count - 1);
                    UpdateCurrent();
                }
                else
                {
                    Close();
                }
            }
        }
        catch (Exception ex)
        {
            InAppToast.MainWindow?.Error(ex);
        }
    }



    private void Button_Info_Click(object? sender, RoutedEventArgs e)
    {
        if (Current is not { } item)
        {
            return;
        }
        try
        {
            var info = new FileInfo(item.FilePath);
            string text = $"{item.FileName}\n{info.FullName}\n{info.Length / 1024.0:F1} KB\n{item.CreationTime:yyyy-MM-dd HH:mm:ss}";
            InAppToast.MainWindow?.Information(Lang.ImageViewWindow2_ImageInformation, text);
        }
        catch (Exception ex)
        {
            InAppToast.MainWindow?.Error(ex);
        }
    }


}
