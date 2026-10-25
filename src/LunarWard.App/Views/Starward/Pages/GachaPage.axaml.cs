using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

/// <summary>
/// 右侧 Pane 材料卡的占位底色（Starward 用 <c>RarityToGIBgConverter</c> 的稀有度底图 + 图标，
/// LunarWard 没有素材，用同色半透明底占位）。
/// </summary>
/// <remarks>
/// 颜色取自 Starward <c>Features/Gacha/GachaStatsCard.xaml:15-17</c>：
/// 5★ #FFA500 / 4★ #C864E0 / 3★ #5EAAED。
/// </remarks>
public sealed class GachaRarityBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush FiveStar = new(Color.FromArgb(0x4D, 0xFF, 0xA5, 0x00));

    private static readonly SolidColorBrush FourStar = new(Color.FromArgb(0x4D, 0xC8, 0x64, 0xE0));

    private static readonly SolidColorBrush ThreeStar = new(Color.FromArgb(0x4D, 0x5E, 0xAA, 0xED));

    private static readonly SolidColorBrush Other = new(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));


    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            5 => FiveStar,
            4 => FourStar,
            3 => ThreeStar,
            _ => Other,
        };
    }


    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}


public partial class GachaPage : StarwardPage
{
    private GachaPageViewModel? _viewModel;

    private bool _layoutQueued;


    public GachaPage(PageContext context) : base(context)
    {
        InitializeComponent();
        var seed = CaptureMode.GachaSeed
            ?? (CaptureMode.GachaDeleteDialog ? "zzz" : null)
            ?? (CaptureMode.UigfWindow ? "genshin" : null)
            ?? (CaptureMode.UigfExport is null ? null : "genshin")
            ?? (CaptureMode.GachaExport is null ? null : "genshin")
            ?? (CaptureMode.GachaImport is null ? null : "genshin");
        _viewModel = new GachaPageViewModel(
            context,
            seed is "zzz" or "genshin" ? seed : null,
            CaptureMode.Enabled ? new Launch.Database.GachaLogStore(CaptureMode.CreateDatabase()) : null);
        DataContext = _viewModel;

        if (seed is "zzz" or "genshin")
        {
            _viewModel.LoadRecordsForCapture(CreateSeedRecords(seed == "zzz"));
        }

        if (CaptureMode.UigfExport is { } uigfPath)
        {
            _viewModel.ExportUigfTo(uigfPath);
            Console.WriteLine($"EXPORT_OK {uigfPath}");
        }

        if (CaptureMode.GachaExport is { } exportPath)
        {
            _viewModel.ExportJsonTo(exportPath);
            Console.WriteLine($"EXPORT_OK {exportPath}");
        }

        if (CaptureMode.GachaImport is { } importPath)
        {
            int imported = _viewModel.ImportLegacyFrom(importPath);
            Console.WriteLine($"IMPORT_OK {importPath} {imported}");
        }

        if (CaptureMode.GachaDeleteDialog)
        {
            // 等页面挂到窗口再弹，否则 Show 的 owner 还没准备好
            Dispatcher.UIThread.Post(OnDeleteByTimeRequested, DispatcherPriority.Loaded);
        }

        if (CaptureMode.UigfWindow)
        {
            Dispatcher.UIThread.Post(OnUigfRequested, DispatcherPriority.Loaded);
        }

        _viewModel.DeleteByTimeRequested += OnDeleteByTimeRequested;
        _viewModel.UigfRequested += OnUigfRequested;
        _viewModel.ExportJsonRequested += OnExportJsonRequested;
        _viewModel.ImportLegacyRequested += OnImportLegacyRequested;
        _viewModel.Pools.CollectionChanged += OnPoolsChanged;
        SizeChanged += OnSizeChanged;
        QueueStatsCardLayout();
    }


    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => QueueStatsCardLayout();


    /// <summary>
    /// 导出旧格式（UIGF v3.0 / SRGF v1.0）：先弹保存对话框再写文件（Starward 用 FileSavePicker）。
    /// </summary>
    private async void OnExportJsonRequested()
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = _viewModel.Strings["Gacha.ExportAs"],
                SuggestedFileName = _viewModel.SuggestedExportFileName,
                DefaultExtension = "json",
                FileTypeChoices =
                [
                    new FilePickerFileType("JSON") { Patterns = ["*.json"] },
                ],
            });

            string? path = file?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                _viewModel.ExportJsonTo(path);
            }
        }
        catch (Exception ex)
        {
            _viewModel.NotifyError(ex.Message);
        }
    }


    /// <summary>
    /// 旧格式导入（UIGF v3.0 / JSON）：先弹打开对话框再读文件（Starward <c>ImportGachaLogCommand</c>）。
    /// </summary>
    private async void OnImportLegacyRequested()
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _viewModel.Strings["Gacha.ImportFromLegacy"],
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("JSON") { Patterns = ["*.json"] },
                ],
            });

            string? path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (!string.IsNullOrWhiteSpace(path))
            {
                _viewModel.ImportLegacyFrom(path);
            }
        }
        catch (Exception ex)
        {
            _viewModel.NotifyError(ex.Message);
        }
    }


    /// <summary>
    /// 打开 UIGF v4.2 导出 / 导入窗口（Starward <c>UIGF4GachaWindow</c>）。
    /// </summary>
    private async void OnUigfRequested()
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var window = new Uigf4GachaWindow(Context, _viewModel.ArchiveStore);

        if (CaptureMode.UigfWindow)
        {
            if (CaptureMode.UigfImportPage)
            {
                window.ShowImportPageForCapture();
            }
            window.Show(owner);
            window.CaptureSelf(CaptureMode.Path + ".dialog.png", 600);
            return;
        }

        await window.ShowDialog(owner);
    }


    /// <summary>
    /// 打开「删除指定时间段」对话框（Starward <c>DeleteGachaLogDialog</c>）。
    /// </summary>
    private async void OnDeleteByTimeRequested()
    {
        if (_viewModel is null || !_viewModel.HasRecords)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new DeleteGachaLogDialog(
            Context,
            _viewModel.RecordsSnapshot,
            _viewModel.SelectUid,
            _viewModel.GameKind);

        if (CaptureMode.GachaDeleteDialog)
        {
            dialog.Show(owner);
            dialog.CaptureSelf(CaptureMode.Path + ".dialog.png", 600);
            return;
        }

        await dialog.ShowDialog(owner);

        if (dialog.Deleted is var (uid, begin, end))
        {
            _viewModel.ApplyDeletedRange(uid, begin, end);
        }
    }


    /// <summary>
    /// 自检样例：造出「S 平均 / S 统计 / A 统计 / B 统计」都有内容的记录。
    /// </summary>
    /// <remarks>
    /// 保底长度按 60~90 抽分布，5★ 里每隔一发标 UP（<c>is_up=1</c>），
    /// 这样 ZZZ 卡的「 / UP」与「 / 平均抽数 (UP 数量)」两段后缀都能截到。
    /// </remarks>
    private static IReadOnlyList<Launch.GachaRecord> CreateSeedRecords(bool zzz)
    {
        var pools = zzz ? new[] { 2, 3 } : new[] { 200, 301 };
        var records = new List<Launch.GachaRecord>();
        long id = 1_0000_0000_0000;
        var start = DateTime.Now.AddDays(-30);

        foreach (int pool in pools)
        {
            int pity = 0;
            int pulls = 0;
            int fiveCount = 0;
            for (int i = 0; i < 90; i++)
            {
                pity++;
                pulls++;
                id++;
                int rank = pity >= 74 ? 5 : pity % 10 == 0 ? 4 : 3;
                if (rank == 5)
                {
                    fiveCount++;
                    pity = 0;
                }

                var name = rank switch
                {
                    5 => zzz ? (fiveCount % 2 == 1 ? "耀嘉音" : "月城柳") : "芙宁娜",
                    4 => zzz ? "苍角" : "夏沃蕾",
                    _ => zzz ? "音擎材料" : "祭礼剑",
                };
                records.Add(new Launch.GachaRecord
                {
                    Id = id,
                    Name = name,
                    ItemType = rank == 5 ? (zzz ? "音擎" : "角色") : "材料",
                    RankType = rank,
                    GachaType = pool,
                    GachaTypeName = zzz ? "独家频段" : "角色活动祈愿",
                    Time = start.AddMinutes(records.Count * 7),
                    Uid = 1_0000_0001,
                    IsUp = rank == 5 && fiveCount % 2 == 1,
                });
                if (pulls >= 90)
                {
                    break;
                }
            }
        }

        return records;
    }


    private void OnPoolsChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueStatsCardLayout();


    private void QueueStatsCardLayout()
    {
        if (_layoutQueued)
        {
            return;
        }

        _layoutQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _layoutQueued = false;
            UpdateStatsCardLayout();
        }, DispatcherPriority.Loaded);
    }


    /// <summary>
    /// 统计卡宽度 = (ScrollViewer 宽 - 左右 20 边距 - 卡间距 12) / 卡数，最小 262。
    /// </summary>
    /// <remarks>
    /// Starward GachaLogPage.xaml.cs:393-413 的 <c>UpdateGachaStatsCardLayout</c>。
    /// 宽度写进页面资源 <c>StarGachaStatsCardWidth</c>，卡片用 DynamicResource 取。
    /// </remarks>
    private void UpdateStatsCardLayout()
    {
        var count = _viewModel?.Pools.Count ?? 0;
        if (count <= 0)
        {
            return;
        }

        var available = ScrollViewer_GachaStats.Bounds.Width;
        if (available <= 0)
        {
            return;
        }

        var cardWidth = Math.Clamp((available - 40 - (count - 1) * 12) / count, 262, double.MaxValue);
        Resources["StarGachaStatsCardWidth"] = cardWidth;
    }


    /// <summary>复制抽卡链接（Starward <c>CopyUrlCommand</c>；剪贴板只能从视图层取）。</summary>
    private async void OnCopyUrlClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            _viewModel.NotifyError("Clipboard is not available on this platform.");
            return;
        }

        try
        {
            await clipboard.SetTextAsync(_viewModel.GachaUrl);
            _viewModel.NotifyUrlCopied();
        }
        catch (Exception ex)
        {
            _viewModel.NotifyError(ex.Message);
        }
    }


    /// <summary>「输入 URL」菜单项：打开 URL 输入 Flyout（Starward 是 InputUrlCommand 的对话框）。</summary>
    private void OnShowInputUrlFlyout(object? sender, RoutedEventArgs e)
    {
        // 先让 MenuFlyout 自己关掉，再弹输入 Flyout，避免两个弹层叠在一起
        Dispatcher.UIThread.Post(
            () => Button_InputUrlHost.Flyout?.ShowAt(Button_UpdateRecordsMenu),
            DispatcherPriority.Background);
    }


    /// <summary>URL 输入 Flyout 里的操作按钮：执行命令后收起 Flyout。</summary>
    private void OnCloseInputUrlFlyout(object? sender, RoutedEventArgs e)
        => Button_InputUrlHost.Flyout?.Hide();


    protected override void DisposeViewModel(PageContext context)
    {
        SizeChanged -= OnSizeChanged;
        if (_viewModel is not null)
        {
            _viewModel.Pools.CollectionChanged -= OnPoolsChanged;
            _viewModel.Dispose();
        }

        _viewModel = null;
    }
}
