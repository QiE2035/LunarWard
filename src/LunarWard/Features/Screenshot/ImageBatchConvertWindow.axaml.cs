using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Codec;
using LunarWard.Helpers;
using LunarWard.Language;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 图片批量转换（上游 <c>Features/Screenshot/ImageBatchConvertWindow.xaml(.cs)</c>）。
/// <para>
/// Linux 差异（docs/09 §8.3）：WinUI 的 <c>Windows.Graphics.Imaging</c>（WIC 编解码）与
/// Starward.Codec 的 AVIF/JXL 编码器换成 <see cref="ImageLoader"/>（Skia + 外部 avifdec/djxl）
/// 与 <see cref="ImageSaver"/>（PNG 自写编码器 + 外部 avifenc/cjxl）；拖拽改用 Avalonia 的
/// <see cref="Avalonia.Input.IDataTransfer"/>；打开所在目录用 <c>xdg-open</c>。
/// </para>
/// </summary>
public sealed partial class ImageBatchConvertWindow : Window
{

    private readonly ILogger<ImageBatchConvertWindow> _logger = AppConfig.GetLogger<ImageBatchConvertWindow>();


    private IBrush _deltaAddBrush = Brushes.IndianRed;

    private IBrush _deltaDecreseBrush = Brushes.SeaGreen;

    private IBrush _deltaZeroBrush = Brushes.Gray;


    public ImageBatchConvertWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadThemeBrushes();
    }


    private void LoadThemeBrushes()
    {
        // 上游从 App.Current.Resources 取 SystemFillColorCriticalBrush / SystemFillColorSuccessBrush / TextFillColorSecondaryBrush
        if (this.TryFindResource("StarErrorBrush", out object? error) && error is IBrush errorBrush)
        {
            _deltaAddBrush = errorBrush;
        }
        if (this.TryFindResource("StarSuccessBrush", out object? success) && success is IBrush successBrush)
        {
            _deltaDecreseBrush = successBrush;
        }
        if (this.TryFindResource("StarTextSecondaryBrush", out object? secondary) && secondary is IBrush secondaryBrush)
        {
            _deltaZeroBrush = secondaryBrush;
        }
    }


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


    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try
        {
            _cancellationTokenSource?.Cancel();
            _itemsDict.Clear();
            ImageConvertItems.Clear();
        }
        catch { }
    }



    public void Activate(List<ScreenshotItem> items)
    {
        base.Activate();
        try
        {
            foreach (ScreenshotItem item in items)
            {
                if (ScreenshotHelper.IsSupportedExtension(Path.GetExtension(item.FilePath)))
                {
                    if (!_itemsDict.ContainsKey(item.FilePath))
                    {
                        ImageConvertItem convertItem = new(item.FilePath);
                        ImageConvertItems.Add(convertItem);
                        _itemsDict[item.FilePath] = convertItem;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import image files failed");
        }
    }



    private CancellationTokenSource? _cancellationTokenSource;

    private Dictionary<string, ImageConvertItem> _itemsDict = new();

    public ObservableCollection<ImageConvertItem> ImageConvertItems { get; set; } = new();

    public string OutputFolder { get; set => SetProperty(ref field, value); } = Lang.ImageBatchConvertWindow_OriginalFileFolder;

    public int TotalCount { get; set => SetProperty(ref field, value); }

    public int SuccessCount { get; set => SetProperty(ref field, value); }

    public int ErrorCount { get; set => SetProperty(ref field, value); }

    public bool DisplayInfo { get; set => SetProperty(ref field, value); }

    public bool IsConverting { get; set => SetProperty(ref field, value); }

    public long TotalSourceFileSize { get; set => SetProperty(ref field, value); }

    public long TotalOutputFileSize { get; set => SetProperty(ref field, value); }

    public string InfoText => $"{Lang.Common_Success} {SuccessCount}/{TotalCount}  {Lang.Common_Failed} {ErrorCount}  {SizeToString(TotalSourceFileSize)} → {SizeToString(TotalOutputFileSize)}";


    private void UpdateInfoText() => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(nameof(InfoText)));



    private async void Button_Import_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            List<string> files = await FileDialogHelper.PickMultipleFilesAsync(TopLevel.GetTopLevel(this)!);
            foreach (string file in files)
            {
                if (ScreenshotHelper.IsSupportedExtension(Path.GetExtension(file)))
                {
                    if (!_itemsDict.ContainsKey(file))
                    {
                        ImageConvertItem convertItem = new(file);
                        ImageConvertItems.Add(convertItem);
                        _itemsDict[file] = convertItem;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import image files failed");
        }
    }


    private async void Button_OutputFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? folder = await FileDialogHelper.PickFolderAsync(TopLevel.GetTopLevel(this)!);
            if (Directory.Exists(folder))
            {
                OutputFolder = folder;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pick output folder failed");
        }
    }



    private async void Button_StartConvert_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            DisableControls();
            if (_cancellationTokenSource is null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                await ConvertInternalAsync(_cancellationTokenSource.Token);
            }
            else
            {
                _cancellationTokenSource.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Convert images canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Convert images failed");
        }
        finally
        {
            _cancellationTokenSource = null;
            RestoreControls();
        }
    }


    private void Button_Stop_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;
            RestoreControls();
            DisplayInfo = false;
            foreach (ImageConvertItem item in ImageConvertItems)
            {
                item.Converting = false;
                item.ConvertError = false;
                item.ConvertSuccess = false;
                item.ErrorMessage = null!;
                item.OutputFileName = null!;
                item.OutputFilePath = null!;
                item.OutputFileSize = 0;
                item.OutputFileSizeText = null!;
                item.FileDeltaPercent = null!;
                item.FileDeltaTextBrush = null!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stop converting images failed");
        }
    }


    private void Button_Clear_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _itemsDict.Clear();
            ImageConvertItems.Clear();
            TotalCount = 0;
            SuccessCount = 0;
            ErrorCount = 0;
            DisplayInfo = false;
            IsConverting = false;
            TotalSourceFileSize = 0;
            TotalOutputFileSize = 0;
        }
        catch { }
    }


    private void ListView_ImageConvertItems_DragOver(object? sender, DragEventArgs e)
    {
        try
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        catch { }
    }


    private void ListView_ImageConvertItems_Drop(object? sender, DragEventArgs e)
    {
        try
        {
            IStorageItem[]? items = e.DataTransfer.TryGetFiles();
            if (items is null)
            {
                return;
            }
            foreach (IStorageItem item in items)
            {
                if (item.TryGetLocalPath() is not { } path)
                {
                    continue;
                }
                if (File.Exists(path))
                {
                    AddFile(path);
                }
                else if (Directory.Exists(path))
                {
                    foreach (string file in Directory.GetFiles(path))
                    {
                        AddFile(file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drop images");
        }

        void AddFile(string file)
        {
            if (ScreenshotHelper.IsSupportedExtension(Path.GetExtension(file)) && !_itemsDict.ContainsKey(file))
            {
                ImageConvertItem convertItem = new(file);
                ImageConvertItems.Add(convertItem);
                _itemsDict[file] = convertItem;
            }
        }
    }


    private void HyperlinkButton_SourceFileName_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ImageConvertItem item })
        {
            OpenInFileManager(item.SourceFilePath);
        }
    }


    private void HyperlinkButton_OutputFileName_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ImageConvertItem item })
        {
            OpenInFileManager(item.OutputFilePath);
        }
    }


    /// <summary>
    /// 上游用 FolderLauncherOptions.ItemsToSelect 选中文件；Linux 打开所在目录（docs/09 §8.3）。
    /// </summary>
    private void OpenInFileManager(string? path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }
            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Process.Start(new ProcessStartInfo { FileName = "xdg-open", ArgumentList = { folder }, UseShellExecute = false });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open file in file manager failed");
        }
    }



    private void DisableControls()
    {
        Button_Import.IsEnabled = false;
        Button_OutputFolder.IsEnabled = false;
        Button_Clear.IsEnabled = false;
        ComboBox_OutputFormat.IsEnabled = false;
        ComboBox_OutputFileExists.IsEnabled = false;
        Slider_Quality.IsEnabled = false;
        Button_StartConvert.Content = Lang.DownloadGamePage_Pause;
    }


    private void RestoreControls()
    {
        Button_Import.IsEnabled = true;
        Button_OutputFolder.IsEnabled = true;
        Button_Clear.IsEnabled = true;
        ComboBox_OutputFormat.IsEnabled = true;
        ComboBox_OutputFileExists.IsEnabled = true;
        Slider_Quality.IsEnabled = true;
        Button_StartConvert.Content = Lang.ImageBatchConvertWindow_StartConvert;
    }




    private string _format = "";

    private string? _outputFolder;

    private int _overwriteMode;

    private int _quality;


    private async Task ConvertInternalAsync(CancellationToken cancellationToken = default)
    {
        TotalCount = 0;
        SuccessCount = 0;
        ErrorCount = 0;
        TotalSourceFileSize = 0;
        TotalOutputFileSize = 0;
        UpdateInfoText();
        if (ImageConvertItems.Count == 0)
        {
            DisplayInfo = false;
            return;
        }
        _format = ComboBox_OutputFormat.SelectedIndex switch
        {
            0 => ".jpg",
            1 => ".png",
            2 => ".avif",
            3 => ".jxl",
            _ => "",
        };
        if (string.IsNullOrWhiteSpace(_format))
        {
            // 使下拉菜单可选
            ComboBox_OutputFormat.IsDropDownOpen = true;
            ComboBox_OutputFormat.IsDropDownOpen = false;
            ComboBox_OutputFormat.IsDropDownOpen = true;
            return;
        }
        _overwriteMode = ComboBox_OutputFileExists.SelectedIndex switch
        {
            -1 => 0,
            _ => ComboBox_OutputFileExists.SelectedIndex,
        };

        TotalCount = ImageConvertItems.Count;
        DisplayInfo = true;
        UpdateInfoText();

        _outputFolder = null;
        if (Path.IsPathFullyQualified(OutputFolder) && Directory.Exists(OutputFolder))
        {
            _outputFolder = OutputFolder;
        }
        _quality = (int)Math.Clamp(Slider_Quality.Value, 0, 100);

        IsConverting = true;
        try
        {
            foreach (ImageConvertItem item in ImageConvertItems)
            {
                await ConvertImageAsync(item, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            IsConverting = false;
        }
    }


    private async Task ConvertImageAsync(ImageConvertItem item, CancellationToken cancellationToken = default)
    {
        if (item.ConvertSuccess)
        {
            SuccessCount++;
            return;
        }
        item.ConvertError = false;
        try
        {
            item.Converting = true;
            if (item.SourceExtension == _format)
            {
                string outputPath = GetOutputPath(item, true);
                if (!File.Exists(outputPath))
                {
                    File.Copy(item.SourceFilePath, outputPath);
                }
                item.OutputFilePath = outputPath;
            }
            else
            {
                await ConvertAsync(item, cancellationToken);
            }
            item.OutputFileName = Path.GetFileName(item.OutputFilePath);
            item.OutputFileSize = new FileInfo(item.OutputFilePath).Length;
            item.OutputFileSizeText = $"{item.OutputFileSize / 1024:N0} KB";
            long delta = item.OutputFileSize - item.SourceFileSize;
            double percent = item.SourceFileSize == 0 ? 0 : (double)delta / item.SourceFileSize;
            item.FileDeltaPercent = percent switch
            {
                > 0 => $"+{percent:P0}",
                < 0 => $"{percent:P0}",
                _ => "0%",
            };
            item.FileDeltaTextBrush = delta switch
            {
                > 0 => _deltaAddBrush,
                < 0 => _deltaDecreseBrush,
                _ => _deltaZeroBrush,
            };
            item.ConvertSuccess = true;
            SuccessCount++;
            TotalSourceFileSize += item.SourceFileSize;
            TotalOutputFileSize += item.OutputFileSize;
            UpdateInfoText();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Convert image failed: {file}", item.SourceFilePath);
            item.ConvertError = true;
            item.ErrorMessage = ex is UnauthorizedAccessException ? Lang.ImageBatchConvertWindow_NoReadOrWritePermission : Lang.ImageBatchConvertWindow_ConvertFailed;
            ErrorCount++;
            UpdateInfoText();
        }
        finally
        {
            item.Converting = false;
        }
    }


    /// <summary>
    /// 上游按源/目标扩展名分了 4 个转换函数（avifenc/cjxl/avifdec/djxl/WIC）；
    /// 这里统一走 <see cref="ImageLoader"/> + <see cref="ImageSaver"/>（内部同样是这些外部工具）。
    /// </summary>
    private async Task ConvertAsync(ImageConvertItem item, CancellationToken cancellationToken)
    {
        string outputPath = GetOutputPath(item);
        if (File.Exists(outputPath) && _overwriteMode is 0)
        {
            item.OutputFilePath = outputPath;
            return;
        }
        using ImageInfo imageInfo = await ImageLoader.LoadImageAsync(item.SourceFilePath, cancellationToken);
        byte[] xmpData = ScreenCaptureService.BuildXMPMetadata(item.SourceFileTime);
        switch (_format)
        {
            case ".avif":
                {
                    await using FileStream fs = File.Create(outputPath);
                    await using MemoryStream ms = new();
                    await ImageSaver.SaveAsAvifAsync(imageInfo.CanvasBitmap, ms, imageInfo.ColorPrimaries, _quality, xmpData, imageInfo.HDR);
                    ms.Position = 0;
                    await ms.CopyToAsync(fs, CancellationToken.None);
                    break;
                }
            case ".jxl":
                {
                    await using FileStream fs = File.Create(outputPath);
                    await using MemoryStream ms = new();
                    // 上游此窗口直接把 0-100 的 quality 交给 JxlEncoder；本仓库 ImageSaver 的参数是 distance，
                    // 按 ScreenCaptureService 的质量映射线性换：quality 100 → 0（无损），90 → 1，80 → 2。
                    float distance = (100 - _quality) * 0.1f;
                    await ImageSaver.SaveAsJxlAsync(imageInfo.CanvasBitmap, ms, imageInfo.ColorPrimaries, distance, xmpData, imageInfo.HDR);
                    ms.Position = 0;
                    await ms.CopyToAsync(fs, CancellationToken.None);
                    break;
                }
            case ".png":
                {
                    await using FileStream fs = File.Create(outputPath);
                    await ImageSaver.SaveAsPngAsync(imageInfo.CanvasBitmap, fs, imageInfo.ColorPrimaries, xmpData, true);
                    break;
                }
            case ".jpg":
                {
                    using SKBitmap bitmap = ToRgba8888(imageInfo.CanvasBitmap);
                    using SKImage image = SKImage.FromBitmap(bitmap);
                    using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, _quality);
                    await using FileStream fs = File.Create(outputPath);
                    data.SaveTo(fs);
                    break;
                }
            default:
                throw new NotSupportedException($"Unsupported output format '{_format}'.");
        }
        item.OutputFilePath = outputPath;
    }


    /// <summary>
    /// 浮点/16bit 位图（HDR）无法直接编 JPEG，先画到 8bit RGBA 画布上（与上游 WIC 转 SDR 一致）。
    /// </summary>
    private static SKBitmap ToRgba8888(SKBitmap source)
    {
        if (source.ColorType is SKColorType.Rgba8888 or SKColorType.Bgra8888)
        {
            return source.Copy();
        }
        SKBitmap target = new(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (SKCanvas canvas = new(target))
        {
            canvas.DrawBitmap(source, 0, 0);
        }
        return target;
    }



    private string GetOutputPath(ImageConvertItem item, bool doNotRename = false)
    {
        string name = Path.GetFileNameWithoutExtension(item.SourceFilePath);
        string folder = _outputFolder ?? Path.GetDirectoryName(item.SourceFilePath)!;
        string outputPath = Path.Combine(folder, name + _format);
        if (doNotRename || _overwriteMode is 0 or 1)
        {
            return outputPath;
        }
        int i = 0;
        while (File.Exists(outputPath))
        {
            outputPath = Path.Combine(folder, $"{name}_{++i}{_format}");
        }
        return outputPath;
    }


    public static string SizeToString(long size)
    {
        const double MB = 1 << 20;
        return $"{size / MB:F2} MB";
    }

}



public class ImageConvertItem : ObservableObject
{

    public string SourceFilePath { get; set; }

    public string SourceFileName { get; set; }

    public string SourceExtension { get; set; }

    public long SourceFileSize { get; set; }

    public string SourceFileSizeText { get; set; }

    public DateTimeOffset SourceFileTime { get; set; }


    public bool Converting { get => field; set => SetProperty(ref field, value); }

    public bool ConvertSuccess { get => field; set => SetProperty(ref field, value); }

    public bool ConvertError { get; set => SetProperty(ref field, value); }


    public string OutputFilePath { get; set; }

    public string OutputFileName { get; set => SetProperty(ref field, value); }

    public long OutputFileSize { get; set => SetProperty(ref field, value); }

    public string OutputFileSizeText { get => field; set => SetProperty(ref field, value); }

    public IBrush FileDeltaTextBrush { get; set => SetProperty(ref field, value); }

    public string FileDeltaPercent { get; set => SetProperty(ref field, value); }


    public string ErrorMessage { get; set => SetProperty(ref field, value); }


    public ImageConvertItem(string filePath)
    {
        SourceFilePath = filePath;
        SourceFileName = Path.GetFileName(filePath);
        SourceExtension = Path.GetExtension(filePath).ToLowerInvariant();
        FileInfo fileInfo = new(filePath);
        SourceFileSize = fileInfo.Length;
        SourceFileSizeText = $"{SourceFileSize / 1024:N0} KB";
        if (ScreenshotItem.TryParseCreationTime(SourceFileName, out DateTime time))
        {
            SourceFileTime = new DateTimeOffset(time);
        }
        else
        {
            SourceFileTime = fileInfo.CreationTime;
        }
    }

}
