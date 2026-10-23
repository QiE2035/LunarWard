using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 截图设置页（复刻 Starward <c>ScreenshotSetting.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 这里能改截图目录、格式（PNG/AVIF/JPEG XL）、色彩管理与"自动转 SDR"。
/// LunarWard <b>不改写游戏截图</b>：目录由游戏决定（<see cref="ScreenshotService"/>），
/// 格式由游戏决定，也没有 WIC 转码。因此这一页只读展示 + 打开目录。
/// </para>
/// <para>格式/排序等"本该可配"的项保留成只读行，是为了排版与 Starward 对齐。</para>
/// </remarks>
public sealed partial class SettingScreenshotViewModel : SettingSectionViewModel
{
    private readonly ScreenshotService _service = new();

    private readonly string _gameDirectory;

    private readonly string _gameKind;


    public SettingScreenshotViewModel(PageContext context, bool showDivider = false)
        : base(context, SettingSectionKind.Screenshot, "Settings.Screenshot", "\uE91B", showDivider)
    {
        _gameDirectory = ProfileConfig.ExpandPath(context.Profile.Profile.Path);
        _gameKind = context.Profile.Profile.Game;
        DirectoryPath = ScreenshotService.GetDirectory(_gameDirectory, _gameKind);
        Reload();
    }


    public string DirectoryPath { get; }

    public string FormatValue => Strings["Settings.Screenshot.FormatValue"];

    public string SortValue => Strings["Settings.Screenshot.SortValue"];


    [ObservableProperty]
    private string _countText = "";


    [ObservableProperty]
    private bool _isEmpty = true;


    [ObservableProperty]
    private string? _status;


    /// <summary>重新统计截图数量与占用（进入页面时调用）。</summary>
    public void Reload()
    {
        try
        {
            var (count, bytes) = _service.Summarize(_gameDirectory, _gameKind);
            IsEmpty = count == 0;
            CountText = count == 0
                ? Strings["Settings.Screenshot.Empty"]
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Strings["Settings.Screenshot.Count"],
                    count,
                    bytes / 1048576.0);
        }
        catch (Exception ex)
        {
            IsEmpty = true;
            CountText = Strings["Settings.Screenshot.Empty"];
            Status = ex.Message;
        }
    }


    [RelayCommand]
    private void OpenDirectory()
    {
        var target = DirectoryPath;
        if (!Directory.Exists(target))
        {
            // 目录还没生成（没在游戏里截过图）：退到游戏目录，至少能看到位置
            var parent = Directory.GetParent(target)?.FullName;
            if (parent is null || !Directory.Exists(parent))
            {
                Status = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings["Screenshot.DirectoryMissing"],
                    target);
                return;
            }

            target = parent;
        }

        Status = SettingShellActions.TryOpen(target) is { } error
            ? string.Format(CultureInfo.CurrentCulture, Strings["Settings.Common.OpenFailed"], error)
            : null;
    }


    [RelayCommand]
    private async Task CopyPathAsync()
    {
        Status = await SettingShellActions.TryCopyAsync(DirectoryPath).ConfigureAwait(true) is { } error
            ? error
            : Strings["Settings.Common.Copied"];
    }
}
