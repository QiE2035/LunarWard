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
        // 没配游戏目录时不能调 GetDirectory：它会拼出相对路径 "ScreenShot"，看起来像工作目录
        HasGameDirectory = !string.IsNullOrEmpty(_gameDirectory);
        // Starward 的文件夹胶囊总是有值；没配游戏目录时用占位文案，保持胶囊结构
        DirectoryPath = HasGameDirectory
            ? ScreenshotService.GetDirectory(_gameDirectory, _gameKind)
            : Strings["Settings.Screenshot.NoGameDirShort"];
        Reload();
    }


    /// <summary>档案里是否已经指定游戏目录。</summary>
    public bool HasGameDirectory { get; }

    public string DirectoryPath { get; }

    [ObservableProperty]
    private string _countText = "";


    /// <summary>重新统计截图数量与占用（进入页面时调用）。</summary>
    public void Reload()
    {
        if (!HasGameDirectory)
        {
            CountText = Strings["Settings.Screenshot.NoGameDirectory"];
            return;
        }

        try
        {
            var (count, bytes) = _service.Summarize(_gameDirectory, _gameKind);
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
            CountText = Strings["Settings.Screenshot.Empty"];
            Status = ex.Message;
        }
    }


    [RelayCommand]
    private void OpenDirectory()
    {
        if (!HasGameDirectory)
        {
            Status = Strings["Settings.Screenshot.NoGameDirectory"];
            return;
        }

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
        if (!HasGameDirectory)
        {
            Status = Strings["Settings.Screenshot.NoGameDirectory"];
            return;
        }

        Status = await SettingShellActions.TryCopyAsync(DirectoryPath).ConfigureAwait(true) is { } error
            ? error
            : Strings["Settings.Common.Copied"];
    }
}
