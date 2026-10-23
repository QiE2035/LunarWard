using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.ViewModels;
using LunarWard.App.Views.Starward;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 游戏截图页（复刻 Starward <c>ScreenshotPage2.xaml</c>：顶部工具条 + 左侧列表 + 右侧大图）。
/// </summary>
/// <remarks>
/// <para>
/// 截图发现/导出逻辑全部复用 <see cref="ScreenshotViewModel"/>（不重写），本类只补
/// 页面层需要的东西：本地化摘要、空态判定、打开目录/复制路径、以及导出后的提示。
/// </para>
/// <para>Starward 的 <c>ItemsView</c> 是磁贴墙；本页按 UI 合同改成「左列表 + 右预览」。</para>
/// </remarks>
public sealed partial class ScreenshotPageViewModel : StarwardPageViewModel
{
    private readonly ScreenshotViewModel _browser;

    private readonly ScreenshotService _service = new();

    private readonly string _gameDirectory;

    private readonly string _gameKind;


    public ScreenshotPageViewModel(PageContext context) : base(context)
    {
        _gameDirectory = ProfileConfig.ExpandPath(context.Profile.Profile.Path);
        _gameKind = context.Profile.Profile.Game;

        _browser = new ScreenshotViewModel(context.Profile);
        _browser.Items.CollectionChanged += OnItemsCollectionChanged;
        _browser.PropertyChanged += OnBrowserPropertyChanged;

        Localization.AppStrings.Instance.PropertyChanged += OnStringsChanged;

        UpdateSummary();
    }


    /// <summary>复用旧窗口的截图浏览 VM（Items / Selected / 命令）。</summary>
    public ScreenshotViewModel Browser => _browser;

    /// <summary>截图目录（游戏写入的绝对路径）。</summary>
    public string DirectoryPath => _browser.Directory;

    /// <summary>档案里还没配游戏目录：此时没有可信的截图路径，界面给引导文案而不是假的相对路径。</summary>
    public bool HasGameDirectory => !string.IsNullOrWhiteSpace(_gameDirectory);

    /// <summary>工具条上展示的路径（没配目录时给提示文案）。</summary>
    public string DirectoryDisplay => HasGameDirectory
        ? DirectoryPath
        : Strings["Screenshot.NoGameDirectory"];

    public bool HasItems => _browser.Items.Count > 0;

    /// <summary>空态：目录里一张图都没有。</summary>
    public bool IsEmpty => _browser.Items.Count == 0;

    public bool HasSelection => _browser.Selected is not null;

    public bool CanExport => _browser.Selected is not null;

    public bool DirectoryMissing => !System.IO.Directory.Exists(DirectoryPath);


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
        UpdateSummary();
    }


    [RelayCommand]
    private void Refresh()
    {
        ReloadFromDisk();
        ActionText = ErrorText is null
            ? string.Format(CultureInfo.CurrentCulture, Strings["Screenshot.Refreshed"], TotalCount)
            : "";
    }


    [RelayCommand]
    private void ExportSelected()
    {
        _browser.ExportSelectedCommand.Execute(null);

        if (_browser.ErrorText is { } error)
        {
            ErrorText = error;
            ActionText = "";
            return;
        }

        if (_browser.Selected is { } item)
        {
            ErrorText = null;
            ActionText = string.Format(CultureInfo.CurrentCulture, Strings["Screenshot.Exported"], item.FileName);
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
                    ErrorText = string.Format(CultureInfo.CurrentCulture, Strings["Screenshot.DirectoryMissing"], path);
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
            ErrorText = ex.Message;
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
                ActionText = Strings["Screenshot.Copied"];
                ErrorText = null;
            }
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(HasGameDirectory));
        OnPropertyChanged(nameof(DirectoryDisplay));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(DirectoryMissing));
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
        if (e.PropertyName is nameof(ScreenshotViewModel.Selected))
        {
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanExport));
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
        _browser.Items.CollectionChanged -= OnItemsCollectionChanged;
        _browser.PropertyChanged -= OnBrowserPropertyChanged;
        Localization.AppStrings.Instance.PropertyChanged -= OnStringsChanged;
        base.Dispose();
    }
}
