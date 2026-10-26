using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using LunarWard.Features.Gacha.UIGF;
using LunarWard.Features.Gacha.ZZZGachaToolbox;
using LunarWard.Features.Screenshot;
using LunarWard.Language;
using LunarWard.Frameworks;

namespace LunarWard.Features.Setting;

/// <summary>
/// 工具箱页（复刻 Starward <c>ToolboxSetting.xaml/.xaml.cs</c> 的 180×220 卡片网格）。
/// </summary>
/// <remarks>
/// 上游的卡片就是三个窗口入口：UIGF 抽卡记录导入导出、绝区零抽卡图片、图片查看器，
/// 点击直接 <c>Activate()</c>/打开窗口，不经过外壳导航（见上游 <c>Button_Click</c>）。
/// </remarks>
public sealed partial class ToolboxSetting : PageBase
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


    public ToolboxSetting()
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();
        DataContext = this;
    }


    protected override void OnLoaded()
    {
        base.OnLoaded();
        ToolboxItems =
        [
            new ToolboxItem("\xE794", null, nameof(UIGF4GachaWindow),
                nameof(Lang.ToolboxSetting_GachaRecordsImportExport), "") { Description = "UIGF v4.2" },
            new ToolboxItem(null, null, nameof(ZZZGachaInfoWindow),
                nameof(Lang.ToolboxSetting_ZZZGachaItemImages),
                nameof(Lang.ToolboxSetting_DisplayItemImagesInZZZGachaRecordPage)),
            new ToolboxItem("\xE91B", null, nameof(ImageViewWindow2),
                nameof(Lang.ToolboxSetting_ImageViewer),
                nameof(Lang.ToolboxSetting_ViewOrEditImage)),
        ];
    }


    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        ToolboxItems = null!;
    }


    public List<ToolboxItem>? ToolboxItems { get; set => SetProperty(ref field, value); }


    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Avalonia.Controls.Control { DataContext: ToolboxItem item })
            {
                switch (item.Tag)
                {
                    case nameof(ZZZGachaInfoWindow):
                        new ZZZGachaInfoWindow().Activate();
                        break;
                    case nameof(UIGF4GachaWindow):
                        new UIGF4GachaWindow().Activate();
                        break;
                    case nameof(ImageViewWindow2):
                        OpenImageViewer();
                        break;
                }
            }
        }
        catch
        {
            // 上游同名 try/catch：窗口打不开不打断设置页
        }
    }


    /// <summary>
    /// 图片查看器入口：有截图就打开查看器，没有就打开截图目录。
    /// </summary>
    /// <remarks>
    /// 上游 <c>ImageViewWindow2.ShowWindow(AppWindowId)</c> 依赖 WinUI 的 AppWindowId，
    /// Avalonia 版窗口需要截图集合，因此这里按同一入口语义补一步。
    /// </remarks>
    private void OpenImageViewer()
    {
        string folder = string.IsNullOrWhiteSpace(AppConfig.ScreenshotFolder)
            ? Path.Join(AppConfig.UserDataFolder, "Screenshots")
            : AppConfig.ScreenshotFolder!;
        var files = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Where(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         || x.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || x.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => File.GetLastWriteTimeUtc(x))
                .ToList()
            : [];
        if (files.Count == 0)
        {
            Status = SettingShellActions.TryOpen(folder);
            return;
        }

        var items = new System.Collections.ObjectModel.ObservableCollection<ScreenshotItem>(
            files.Select(x => new ScreenshotItem(x)));
        new ImageViewWindow2(items, 0).Show();
    }


    /// <summary>错误/状态行。</summary>
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
}
