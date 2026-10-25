using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.ViewModels;
using LunarWard.App.Views.Starward;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 游戏截图页（复刻 Starward <c>Features/Screenshot/ScreenshotPage2.xaml</c>：44px 页头 + 磁贴墙）。
/// </summary>
/// <remarks>
/// <para>
/// 截图发现/导出逻辑全部复用 <see cref="ScreenshotViewModel"/>（不重写），本类只补
/// 页面层需要的东西：本地化摘要、空态判定、打开目录/复制路径、导出，以及操作反馈。
/// </para>
/// <para>
/// 视图结构严格照 Starward 源码：标题（20 Bold，左 24）+ 右侧「管理截图文件夹」按钮，
/// 内容区是 <c>ItemsView</c> 磁贴墙（UniformGridLayout）。Starward 的
/// <c>ScreenshotFolderManageDialog</c>（管理截图文件夹对话框）尚未移植，按钮的 Flyout
/// 里放 LunarWard 追加的三行操作，见视图注释。
/// </para>
/// </remarks>
public sealed partial class ScreenshotPageViewModel : StarwardPageViewModel
{
    /// <summary>操作反馈（导出/复制）停留时间。Starward 用 InAppToast，本页还没有 ToastHost。</summary>
    private static readonly TimeSpan FeedbackDuration = TimeSpan.FromSeconds(3.5);


    private readonly ScreenshotViewModel _browser;

    private readonly ScreenshotService _service = new();

    private readonly string _gameDirectory;

    private readonly string _gameKind;

    private readonly DispatcherTimer _feedbackTimer = new() { Interval = FeedbackDuration };


    public ScreenshotPageViewModel(PageContext context) : base(context)
    {
        // 自检：把「游戏目录」换成临时样例目录（只改内存里的 profile，不落盘）
        if (CaptureMode.ScreenshotSeed && CreateSeedDirectory() is { } seedDirectory)
        {
            context.Profile.Profile.Path = seedDirectory;
        }

        _gameDirectory = ProfileConfig.ExpandPath(context.Profile.Profile.Path);
        _gameKind = context.Profile.Profile.Game;

        _browser = new ScreenshotViewModel(context.Profile);
        _browser.Items.CollectionChanged += OnItemsCollectionChanged;
        _browser.PropertyChanged += OnBrowserPropertyChanged;

        Localization.AppStrings.Instance.PropertyChanged += OnStringsChanged;
        _feedbackTimer.Tick += OnFeedbackTimerTick;

        UpdateSummary();
    }


    /// <summary>
    /// 复用旧窗口的截图浏览 VM。磁贴墙直接绑 <c>Browser.Items</c>
    /// （Starward 的 <c>ItemsSource="{x:Bind Screenshots}"</c>）。
    /// </summary>
    /// <summary>
    /// 自检开关 <c>--screenshot-seed</c>：生成四张样例图到临时目录并当作游戏截图目录，
    /// 这样悬停放大、磁贴墙间距都能在无游戏数据的机器上截图验证。
    /// </summary>
    private static string? CreateSeedDirectory()
    {
        string root = System.IO.Path.Join(
            System.IO.Path.GetTempPath(), "lw-shot-seed", "ScreenShot");
        System.IO.Directory.CreateDirectory(root);
        if (System.IO.Directory.GetFiles(root, "*.png").Length >= 4)
        {
            return System.IO.Path.GetDirectoryName(root);
        }

        var gradients = new (Avalonia.Media.Color From, Avalonia.Media.Color To)[]
        {
            (Avalonia.Media.Color.Parse("#FF6B6B"), Avalonia.Media.Color.Parse("#845EC2")),
            (Avalonia.Media.Color.Parse("#00C9A7"), Avalonia.Media.Color.Parse("#005F73")),
            (Avalonia.Media.Color.Parse("#FFC75F"), Avalonia.Media.Color.Parse("#D65DB1")),
            (Avalonia.Media.Color.Parse("#2C73D2"), Avalonia.Media.Color.Parse("#008E9B")),
        };
        for (int i = 0; i < gradients.Length; i++)
        {
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new Avalonia.PixelSize(1920, 1080), new Avalonia.Vector(96, 96));
            using (var context = bitmap.CreateDrawingContext())
            {
                var brush = new Avalonia.Media.LinearGradientBrush
                {
                    StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                    EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
                    GradientStops =
                    {
                        new Avalonia.Media.GradientStop(gradients[i].From, 0),
                        new Avalonia.Media.GradientStop(gradients[i].To, 1),
                    },
                };
                context.FillRectangle(brush,
                    new Avalonia.Rect(0, 0, 1920, 1080));
            }
            using var stream = System.IO.File.Create(
                System.IO.Path.Join(root, $"seed-{i + 1}.png"));
            bitmap.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        return System.IO.Path.GetDirectoryName(root);
    }


    public ScreenshotViewModel Browser => _browser;

    /// <summary>截图目录（游戏写入的绝对路径）。</summary>
    public string DirectoryPath => _browser.Directory;

    /// <summary>档案里还没配游戏目录：此时没有可信的截图路径，界面给引导文案而不是假的相对路径。</summary>
    public bool HasGameDirectory => !string.IsNullOrWhiteSpace(_gameDirectory);

    /// <summary>Flyout 里展示的路径（没配目录时给提示文案）。</summary>
    public string DirectoryDisplay => HasGameDirectory
        ? DirectoryPath
        : Strings["Screenshot.NoGameDirectory"];

    /// <summary>空态：目录里一张图都没有（Starward 的空态是 StackPanel_Emoji）。</summary>
    public bool IsEmpty => _browser.Items.Count == 0;

    /// <summary>
    /// 截图目录不存在（没配游戏目录、或游戏还没生成该目录）。
    /// Starward 的空态文案 <c>ScreenshotPage_ScreenshotFolderDoesNotExist</c> 就是这一种情况。
    /// </summary>
    public bool DirectoryMissing => !HasGameDirectory || !System.IO.Directory.Exists(DirectoryPath);

    /// <summary>目录存在但还没截图：Starward 此时是空白的磁贴墙，这里补一句「还没有截图」。</summary>
    public bool FolderExistsButEmpty => IsEmpty && !DirectoryMissing;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText), nameof(HasSummary))]
    private int totalCount;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private long totalBytes;


    /// <summary>「共 N 张 · X.X MiB」。</summary>
    public string SummaryText => TotalCount == 0
        ? ""
        : string.Format(CultureInfo.CurrentCulture, Strings["Screenshot.Summary"], TotalCount, TotalBytes / 1048576.0);


    public bool HasSummary => TotalCount > 0;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionText))]
    private string actionText = "";


    public bool HasActionText => !string.IsNullOrEmpty(ActionText);


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorText))]
    private string? errorText;


    public bool HasErrorText => !string.IsNullOrEmpty(ErrorText);


    /// <summary>重新扫描目录（导航回来时调用）。</summary>
    public void ReloadFromDisk()
    {
        if (!HasGameDirectory)
        {
            // 没有游戏目录就不扫：否则 ScreenshotService 会拼出相对路径 "ScreenShot"
            _browser.Items.Clear();
            _browser.Selected = null;
            TotalCount = 0;
            TotalBytes = 0;
            ErrorText = null;
            ActionText = "";
            RefreshDerived();
            return;
        }

        _browser.ReloadCommand.Execute(null);
        ErrorText = _browser.ErrorText;
        if (ErrorText is not null)
        {
            RestartFeedbackTimer();
        }

        UpdateSummary();
    }


    [RelayCommand]
    private void Refresh()
    {
        ReloadFromDisk();
        if (ErrorText is null)
        {
            ShowAction(string.Format(CultureInfo.CurrentCulture, Strings["Screenshot.Refreshed"], TotalCount));
        }
    }


    /// <summary>导出磁贴右键菜单里指定的那一张。</summary>
    [RelayCommand]
    private void ExportItem(ScreenshotItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            _service.CopyTo(item.Screenshot, ExportDirectory);
            _browser.Selected = item;
            ShowAction(string.Format(CultureInfo.CurrentCulture, Strings["Screenshot.Exported"], item.FileName));
        }
        catch (Exception ex)
        {
            ReportError(ex.Message);
        }
    }


    /// <summary>导出全部（Starward 没有这个入口，是 LunarWard 保留的旧能力）。</summary>
    [RelayCommand]
    private void ExportAll()
    {
        var items = _browser.Items.ToArray();
        if (items.Length == 0)
        {
            ReportError(Strings["Screenshot.NoItemsToExport"]);
            return;
        }

        try
        {
            foreach (var item in items)
            {
                _service.CopyTo(item.Screenshot, ExportDirectory);
            }

            ShowAction(string.Format(CultureInfo.CurrentCulture, Strings["Screenshot.ExportedAll"], items.Length));
        }
        catch (Exception ex)
        {
            ReportError(ex.Message);
        }
    }


    [RelayCommand]
    private void OpenDirectory()
    {
        if (!HasGameDirectory)
        {
            // 没配游戏目录：引导去游戏设置，而不是弹一个不存在的路径
            Shell.Navigate(ShellPage.GameSetting);
            return;
        }

        try
        {
            var path = DirectoryPath;
            if (!System.IO.Directory.Exists(path))
            {
                // 目录还没生成（没在游戏里截过图）：退到游戏目录，至少让人能看到位置
                var parent = System.IO.Directory.GetParent(path)?.FullName;
                if (parent is null || !System.IO.Directory.Exists(parent))
                {
                    ReportError(string.Format(CultureInfo.CurrentCulture, Strings["Screenshot.DirectoryMissing"], path));
                    return;
                }

                path = parent;
            }

            var info = new ProcessStartInfo { FileName = "xdg-open", UseShellExecute = false };
            info.ArgumentList.Add(path);
            using var process = Process.Start(info);
            ErrorText = null;
        }
        catch (Exception ex)
        {
            ReportError(ex.Message);
        }
    }


    [RelayCommand]
    private async Task CopyPathAsync()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            {
                await window.Clipboard!.SetTextAsync(DirectoryPath);
                ShowAction(Strings["Screenshot.Copied"]);
            }
        }
        catch (Exception ex)
        {
            ReportError(ex.Message);
        }
    }


    /// <summary>视图层报告错误（例如系统图片查看器打不开）。</summary>
    public void ReportError(string message)
    {
        ActionText = "";
        ErrorText = message;
        RestartFeedbackTimer();
    }


    /// <summary>导出目标目录（与旧窗口一致：<c>~/Pictures/LunarWard</c>）。</summary>
    private static string ExportDirectory => Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "LunarWard");


    private void ShowAction(string message)
    {
        ErrorText = null;
        ActionText = message;
        RestartFeedbackTimer();
    }


    private void RestartFeedbackTimer()
    {
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }


    private void OnFeedbackTimerTick(object? sender, EventArgs e)
    {
        _feedbackTimer.Stop();
        ActionText = "";
        ErrorText = null;
    }


    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(HasGameDirectory));
        OnPropertyChanged(nameof(DirectoryDisplay));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(DirectoryMissing));
        OnPropertyChanged(nameof(FolderExistsButEmpty));
    }


    private void UpdateSummary()
    {
        try
        {
            var (count, bytes) = _service.Summarize(_gameDirectory, _gameKind);
            TotalCount = count;
            TotalBytes = bytes;
        }
        catch (Exception)
        {
            TotalCount = _browser.Items.Count;
            TotalBytes = 0;
        }

        RefreshDerived();
    }


    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshDerived();
    }


    private void OnBrowserPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ScreenshotViewModel.Items))
        {
            RefreshDerived();
        }
    }


    private void OnStringsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Item[]" or nameof(Localization.AppStrings.Language))
        {
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(DirectoryDisplay));
        }
    }


    public override void Dispose()
    {
        _feedbackTimer.Stop();
        _feedbackTimer.Tick -= OnFeedbackTimerTick;
        _browser.Items.CollectionChanged -= OnItemsCollectionChanged;
        _browser.PropertyChanged -= OnBrowserPropertyChanged;
        Localization.AppStrings.Instance.PropertyChanged -= OnStringsChanged;
        base.Dispose();
    }
}
