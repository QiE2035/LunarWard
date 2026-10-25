using LunarWard.App.Services;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Threading;
using LunarWard.App.ViewModels;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

/// <summary>
/// 游戏截图页（复刻 Starward <c>Features/Screenshot/ScreenshotPage2.xaml</c>：44px 页头 + 磁贴墙）。
/// </summary>
/// <remarks>
/// 页面本身只有三件事需要 code-behind：磁贴宽度补偿（WrapPanel 没有 UniformGridLayout
/// 的拉伸语义）、磁贴的懒加载/右键菜单、以及点击打开大图（<see cref="ImageViewWindow2"/>）。
/// </remarks>
public partial class ScreenshotPage : StarwardPage
{
    /// <summary>Starward ScreenshotPage2.xaml:179 UniformGridLayout.MinItemWidth。</summary>
    private const double TileMinItemWidth = 204;

    /// <summary>Starward ScreenshotPage2.xaml:178 UniformGridLayout.MinColumnSpacing。</summary>
    private const double TileColumnSpacing = 12;


    private readonly ScreenshotPageViewModel _viewModel;

    /// <summary>「磁贴顶边 Y → 月份」映射（内容坐标，按 Y 升序）；内容高度变化时重建。</summary>
    private readonly List<(double Y, string Month)> _monthMap = [];

    private double _monthMapExtent = -1;

    private DispatcherTimer? _monthHideTimer;


    public ScreenshotPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new ScreenshotPageViewModel(context);
        DataContext = _viewModel;

    }


    public override void OnNavigatedTo()
    {
        // 每次进入页面都重新扫目录：玩家可能刚在游戏里截了新图
        _viewModel.ReloadFromDisk();
        SetupHoverCapture();
        SetupFolderDialogCapture();
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel.Dispose();
    }


    // ── 磁贴墙宽度（等价 Starward UniformGridLayout） ────────────────────

    private void OnWallSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!UpdateTileWidth())
        {
            // 首次 SizeChanged 时 ItemsPanel 可能还没建好，等一次布局完成再算
            Dispatcher.UIThread.Post(() => UpdateTileWidth(), DispatcherPriority.Loaded);
        }
    }


    /// <summary>
    /// 按 UniformGridLayout 的列数公式重算磁贴宽度，让每一行刚好铺满可用宽度。
    /// </summary>
    /// <remarks>
    /// Starward 的 UniformGridLayout 会算 <c>columns = floor((width + spacing) / (minWidth + spacing))</c>
    /// 并把剩余宽度均摊到每一项；Avalonia 的 WrapPanel 只支持固定 ItemWidth，所以在
    /// SizeChanged 时补这一步，视觉结果与 UniformGridLayout 一致（磁贴撑满整行）。
    /// </remarks>
    private bool UpdateTileWidth()
    {
        if (ScreenshotWall.ItemsPanelRoot is not WrapPanel panel)
        {
            return false;
        }

        double available = ScreenshotWall.Bounds.Width;
        if (available <= 0)
        {
            return false;
        }

        int columns = Math.Max(1, (int)Math.Floor((available + TileColumnSpacing) / (TileMinItemWidth + TileColumnSpacing)));
        double itemWidth = Math.Floor((available - (columns - 1) * TileColumnSpacing) / columns);
        if (itemWidth < 1)
        {
            return true;
        }

        if (Math.Abs(panel.ItemWidth - itemWidth) > 0.5)
        {
            panel.ItemWidth = itemWidth;
        }

        return true;
    }


    // ── 月份胶囊（近似 Starward AnnotatedScrollBar 的分组标注） ──────────

    private void OnWallScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroller)
        {
            return;
        }

        if (Math.Abs(scroller.Extent.Height - _monthMapExtent) > 0.5)
        {
            _monthMapExtent = scroller.Extent.Height;
            RebuildMonthMap();
        }

        string? month = ScreenshotMonthIndex.Locate(_monthMap, scroller.Offset.Y);
        if (month is null)
        {
            return;
        }

        MonthIndicatorText.Text = month;
        MonthIndicator.IsVisible = true;
        _monthHideTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        _monthHideTimer.Stop();
        _monthHideTimer.Start();
        _monthHideTimer.Tick -= OnMonthHideTick;
        _monthHideTimer.Tick += OnMonthHideTick;
    }


    private void OnMonthHideTick(object? sender, EventArgs e)
    {
        if (_monthHideTimer is not null)
        {
            _monthHideTimer.Stop();
            _monthHideTimer.Tick -= OnMonthHideTick;
        }

        MonthIndicator.IsVisible = false;
    }


    private void RebuildMonthMap()
    {
        _monthMap.Clear();
        if (ScreenshotWall.ItemsPanelRoot is not WrapPanel panel)
        {
            return;
        }

        foreach (Control child in panel.Children.OfType<Control>())
        {
            if (child.DataContext is ScreenshotItemViewModel item)
            {
                _monthMap.Add((child.Bounds.Y, ScreenshotMonthIndex.Label(item.Screenshot.CapturedAt)));
            }
        }
    }


    // ── 磁贴 ────────────────────────────────────────────────────────────

    private void OnTileAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Border { DataContext: ScreenshotItemViewModel item } tile)
        {
            return;
        }

        // 磁贴真正上屏时才解码缩略图（懒加载，见 ScreenshotItemViewModel.EnsureThumbnail）
        item.EnsureThumbnail();

        // 导出入口放在右键菜单（Starward 没有右键菜单，是 LunarWard 保留的旧能力）
        tile.ContextMenu ??= BuildTileContextMenu(item);
    }


    private ContextMenu BuildTileContextMenu(ScreenshotItemViewModel item)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem
        {
            Header = _viewModel.Strings["Screenshot.ExportThis"],
            Command = _viewModel.ExportItemCommand,
            CommandParameter = item,
        });
        menu.Items.Add(new MenuItem
        {
            Header = _viewModel.Strings["Screenshot.ExportAll"],
            Command = _viewModel.ExportAllCommand,
        });
        return menu;
    }


    /// <summary>
    /// 页头「管理截图文件夹」（Starward <c>ScreenshotPage2.xaml.cs:446-462</c>）：
    /// 弹对话框，保存后把额外目录写回配置并重扫图库。
    /// </summary>
    private void OnManageFoldersClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var profile = _viewModel.Context.Profile.Profile;
        var dialog = new ScreenshotFolderManageDialog(
            _viewModel.DirectoryPath,
            profile.ScreenshotFolders ?? []);
        dialog.Closed += (_, _) =>
        {
            if (dialog.Result is not { } folders)
            {
                return;
            }

            profile.ScreenshotFolders = [.. folders];
            try
            {
                _viewModel.Context.Services.Config.Save(_viewModel.Context.Services.Paths.ConfigFile);
            }
            catch (Exception ex)
            {
                _viewModel.ReportError(ex.Message);
                return;
            }

            _viewModel.ReloadFromDisk();
        };
        _ = DialogService.ShowAsync(dialog, owner);
    }


    private void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ScreenshotItemViewModel item })
        {
            OpenInImageViewer(item);
        }
    }


    /// <summary>
    /// 点击磁贴打开大图。
    /// </summary>
    /// <remarks>
    /// Starward 在这里调用 <c>new ImageViewWindow2().ShowWindowAsync(..., item, Screenshots)</c>
    /// （ScreenshotPage2.xaml.cs:491-504）：支持缩放/拖动/上一张下一张的独立窗口。
    /// LunarWard 已移植为 <see cref="ImageViewWindow2"/>，整份截图集合一起传进去，
    /// 这样窗口里的左右翻页和图库条才有内容。
    /// </remarks>
    private void OpenInImageViewer(ScreenshotItemViewModel item)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var screenshots = _viewModel.Browser.Items.Select(x => x.Screenshot).ToList();
        var window = new ImageViewWindow2(item.Screenshot, screenshots);
        window.Show(owner);
    }

    /// <summary>
    /// 自检开关 <c>--screenshot-hover</c>：等磁贴生成后，把第一块磁贴置为 <c>:pointerover</c>，
    /// 截「缩略图放大到 1.2」的效果（没有真实指针事件时唯一能复现该状态的办法）。
    /// </summary>
    private void SetupHoverCapture()
    {
        if (!CaptureMode.ScreenshotHover)
        {
            return;
        }

        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(120);
                var tile = this.GetVisualDescendants()
                    .OfType<Border>()
                    .FirstOrDefault(x => x.Classes.Contains("tile"));
                if (tile is not null)
                {
                    ((IPseudoClasses)tile.Classes).Set(":pointerover", true);
                    return;
                }
            }
        });
    }


    /// <summary>
    /// 自检开关 <c>--screenshot-folder-dialog</c>：造两个额外目录，打开「管理截图文件夹」对话框并截图。
    /// </summary>
    private void SetupFolderDialogCapture()
    {
        if (!CaptureMode.ScreenshotFolderDialog || CaptureMode.Path is not { } path)
        {
            return;
        }

        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Task.Delay(700);
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            string root = System.IO.Path.Join(System.IO.Path.GetTempPath(), "lw-shot-folders");
            string first = System.IO.Path.Join(root, "Screenshots-2026");
            string second = System.IO.Path.Join(root, "Shared", "Screenshots");
            System.IO.Directory.CreateDirectory(first);
            System.IO.Directory.CreateDirectory(second);

            string inGame = System.IO.Path.Join(root, "ScreenShot");
            System.IO.Directory.CreateDirectory(inGame);
            var dialog = new ScreenshotFolderManageDialog(inGame, [first, second]);
            _ = DialogService.ShowAsync(dialog, owner);
            await Task.Delay(1200);
            CaptureMode.Capture(owner);
        });
    }
}
