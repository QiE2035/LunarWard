using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels;

/// <summary>
/// 截图浏览窗口。
/// </summary>
/// <remarks>
/// 只做"发现 + 预览 + 导出"。不做批量转码（上游用 WIC，Linux 上没必要）。
/// 缩略图用 Avalonia 的 <see cref="Bitmap"/> 解码，解码失败的项静默跳过。
/// </remarks>
public sealed partial class ScreenshotViewModel : ObservableObject
{
    private const int MaxDisplayItems = 300;


    private readonly ScreenshotService _screenshots = new();

    private readonly ProfileItemViewModel _profile;

    private readonly string _gameDirectory;

    private readonly string _gameKind;


    public ScreenshotViewModel(ProfileItemViewModel profile)
    {
        _profile = profile;
        _gameDirectory = ProfileConfig.ExpandPath(profile.Profile.Path);
        _gameKind = profile.Profile.Game;
        Items = new ObservableCollection<ScreenshotItemViewModel>();
        Reload();
    }


    public string Title => _gameKind switch
    {
        "zzz" => "绝区零 · 截图",
        _ => "原神 · 截图",
    };


    public ObservableCollection<ScreenshotItemViewModel> Items { get; }

    public string Directory => ScreenshotService.GetDirectory(_gameDirectory, _gameKind);


    [ObservableProperty]
    private ScreenshotItemViewModel? selected;


    [ObservableProperty]
    private string statusText = "";


    [ObservableProperty]
    private string? errorText;


    partial void OnSelectedChanged(ScreenshotItemViewModel? value)
    {
        // 选中时才解码大图（列表只保留路径，避免一次性解码几百张图）
        value?.EnsurePreview();
    }


    [RelayCommand]
    private void Reload()
    {
        Items.Clear();
        try
        {
            var list = _screenshots.List(_gameDirectory, _gameKind, MaxDisplayItems);
            foreach (var item in list)
            {
                Items.Add(new ScreenshotItemViewModel(item));
            }

            var (count, bytes) = _screenshots.Summarize(_gameDirectory, _gameKind);
            StatusText = count == 0
                ? $"目录里没有截图：{Directory}"
                : $"共 {count} 张，占用 {bytes / 1048576.0:0.0} MiB（显示最近 {Math.Min(count, MaxDisplayItems)} 张）";
            ErrorText = null;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    [RelayCommand]
    private void ExportSelected()
    {
        if (Selected is null)
        {
            ErrorText = "先选一张截图";
            return;
        }

        try
        {
            var target = Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "LunarWard");
            var copied = _screenshots.CopyTo(Selected.Screenshot, target);
            StatusText = $"已导出到 {copied}";
            ErrorText = null;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }
}


/// <summary>一张截图的展示项（延迟解码）。</summary>
public sealed partial class ScreenshotItemViewModel : ObservableObject
{
    /// <summary>
    /// 磁贴缩略图的目标宽度。Starward 的 <c>CachedImage</c> 走
    /// <c>ImageThumbnail.GetThumbnailSize</c>：目标框 412×232
    /// （Features/Codec/ImageThumbnail.cs:265-266），这里用同一档尺寸，
    /// 磁贴（Starward 的 MinItemWidth 204，实际约 204–226 宽）在高 DPI 下也不会糊。
    /// </summary>
    private const int ThumbnailWidth = 412;


    private Bitmap? _preview;

    private Bitmap? _thumbnail;

    /// <summary>0 = 还没开始；1 = 加载中或已结束（失败也算结束，不重试）。</summary>
    private int _thumbnailState;


    public ScreenshotItemViewModel(GameScreenshot screenshot)
    {
        Screenshot = screenshot;
    }


    public GameScreenshot Screenshot { get; }

    public string FileName => Screenshot.FileName;

    public string Time => Screenshot.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss");

    public string SizeText => $"{Screenshot.SizeBytes / 1024.0:0} KiB";

    public string? Uid => ScreenshotService.ExtractUid(Screenshot.FileName);


    [ObservableProperty]
    private Bitmap? preview;


    /// <summary>
    /// 磁贴缩略图。磁贴墙（Starward 的 <c>ItemsView</c>）只绑定本属性：
    /// 未解码时是 <c>null</c>，解码完成后自动通知。
    /// </summary>
    public Bitmap? Thumbnail => _thumbnail;


    /// <summary>
    /// 懒加载磁贴缩略图：只在磁贴真正上屏时调用，并且按 <see cref="ThumbnailWidth"/>
    /// 缩放解码，而不是一次性解码原图（1920×1080 原图一张约 8 MiB）。
    /// </summary>
    /// <remarks>
    /// 解码放到线程池：磁贴墙一次会实例化很多项，同步解码会卡住 UI 线程。
    /// 完成后回 UI 线程赋值（Avalonia <see cref="Bitmap"/> 解码后可以跨线程传递）。
    /// </remarks>
    public void EnsureThumbnail()
    {
        if (Interlocked.Exchange(ref _thumbnailState, 1) != 0)
        {
            return;
        }

        string path = Screenshot.Path;
        _ = Task.Run(() =>
        {
            Bitmap? bitmap = null;
            try
            {
                using var stream = File.OpenRead(path);
                bitmap = Bitmap.DecodeToWidth(stream, ThumbnailWidth, BitmapInterpolationMode.MediumQuality);
            }
            catch (Exception)
            {
                // 坏图 / 不支持的格式：保持 null，磁贴只显示背景色（与 Starward 的 CachedImage 一致）
            }

            Dispatcher.UIThread.Post(() =>
            {
                _thumbnail = bitmap;
                OnPropertyChanged(nameof(Thumbnail));
            });
        });
    }


    /// <summary>解码预览图；失败时保持 <c>null</c>（UI 显示占位）。</summary>
    public void EnsurePreview()
    {
        if (_preview is not null)
        {
            Preview = _preview;
            return;
        }

        try
        {
            _preview = new Bitmap(Screenshot.Path);
            Preview = _preview;
        }
        catch (Exception)
        {
            // 坏图/不支持的格式：静默跳过
            Preview = null;
        }
    }
}
