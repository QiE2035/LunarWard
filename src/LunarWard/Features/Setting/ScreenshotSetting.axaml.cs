using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Frameworks;

namespace LunarWard.Features.Setting;

/// <summary>
/// 截图设置页（复刻 Starward <c>ScreenshotSetting.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 这里能改截图目录、格式（PNG/AVIF/JPEG XL）、色彩管理与"自动转 SDR"。
/// LunarWard <b>不改写游戏截图</b>：格式与转码由游戏决定，也没有 WIC。
/// 目录与上游一致走 <see cref="AppConfig.ScreenshotFolder"/>（默认用户数据目录下的
/// <c>Screenshots</c>），因此这一页只展示目录、数量并支持打开/复制。
/// </para>
/// <para>格式/排序等"本该可配"的项保留成只读行，是为了排版与 Starward 对齐。</para>
/// </remarks>
public sealed partial class ScreenshotSetting : PageBase
{
    public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private static Localization.AppStrings Strings => Localization.AppStrings.Instance;


    public ScreenshotSetting()
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();
        string? folder = AppConfig.ScreenshotFolder;
        DirectoryPath = string.IsNullOrWhiteSpace(folder)
            ? Path.Join(AppConfig.UserDataFolder, "Screenshots")
            : folder;
        Reload();
        DataContext = this;
    }


    /// <summary>截图目录（上游 <c>AppConfig.ScreenshotFolder</c>）。</summary>
    public string DirectoryPath { get; }

    /// <summary>保留 XAML 里的胶囊结构：目录总有值。</summary>
    public bool HasGameDirectory => true;


    private string _countText = "";

    public string CountText
    {
        get => _countText;
        set => SetProperty(ref _countText, value);
    }


    /// <summary>重新统计截图数量与占用（进入页面时调用）。</summary>
    public void Reload()
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
            {
                CountText = Strings["Settings.Screenshot.NoGameDirectory"];
                return;
            }

            var files = Directory.EnumerateFiles(DirectoryPath, "*", SearchOption.AllDirectories)
                .Where(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         || x.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || x.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                         || x.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                .Select(x => new FileInfo(x))
                .ToList();
            long bytes = files.Sum(x => x.Length);
            CountText = files.Count == 0
                ? Strings["Settings.Screenshot.Empty"]
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Strings["Settings.Screenshot.Count"],
                    files.Count,
                    bytes / 1048576.0);
        }
        catch (Exception ex)
        {
            CountText = Strings["Settings.Screenshot.Empty"];
            Status = ex.Message;
        }
    }


    /// <summary>错误/状态行（上游各设置页末尾的 InfoBar 文本）。</summary>
    public string? Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    private string? _status;


    [RelayCommand]
    private void OpenDirectory()
    {
        var target = DirectoryPath;
        if (!Directory.Exists(target))
        {
            try
            {
                Directory.CreateDirectory(target);
            }
            catch
            {
                // 打开失败时下面统一提示
            }
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
