using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
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
    private Bitmap? _preview;


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
