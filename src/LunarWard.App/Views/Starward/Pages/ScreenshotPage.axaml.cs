using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LunarWard.App.ViewModels;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

/// <summary>
/// 游戏截图页（复刻 Starward <c>Features/Screenshot/ScreenshotPage2.xaml</c>：44px 页头 + 磁贴墙）。
/// </summary>
/// <remarks>
/// 页面本身只有三件事需要 code-behind：磁贴宽度补偿（WrapPanel 没有 UniformGridLayout
/// 的拉伸语义）、磁贴的懒加载/右键菜单、以及点击打开大图（Starward 的
/// <c>ImageViewWindow2</c> 尚未移植，见 <see cref="OpenInDefaultViewer"/>）。
/// </remarks>
public partial class ScreenshotPage : StarwardPage
{
    /// <summary>Starward ScreenshotPage2.xaml:179 UniformGridLayout.MinItemWidth。</summary>
    private const double TileMinItemWidth = 204;

    /// <summary>Starward ScreenshotPage2.xaml:178 UniformGridLayout.MinColumnSpacing。</summary>
    private const double TileColumnSpacing = 12;


    private readonly ScreenshotPageViewModel _viewModel;


    public ScreenshotPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new ScreenshotPageViewModel(context);
        DataContext = _viewModel;

        // Flyout 的内容不在按钮的视觉树里，DataContext 不会自然继承，这里显式挂上。
        if (ManageButton.Flyout is Flyout { Content: Control content })
        {
            content.DataContext = _viewModel;
        }
    }


    public override void OnNavigatedTo()
    {
        // 每次进入页面都重新扫目录：玩家可能刚在游戏里截了新图
        _viewModel.ReloadFromDisk();
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


    private void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ScreenshotItemViewModel item })
        {
            OpenInDefaultViewer(item);
        }
    }


    /// <summary>
    /// 点击磁贴打开大图。
    /// </summary>
    /// <remarks>
    /// Starward 在这里调用 <c>new ImageViewWindow2().ShowWindowAsync(..., item, Screenshots)</c>
    /// （ScreenshotPage2.xaml.cs:491-504）：一个支持缩放/拖动/切换上一张下一张的独立窗口。
    /// LunarWard 尚未移植 ImageViewWindow2，也没有可用的应用内大图窗口，因此临时把
    /// 图片交给桌面默认查看器（<c>xdg-open</c>）。这只是等价物，不是最终形态。
    /// </remarks>
    private void OpenInDefaultViewer(ScreenshotItemViewModel item)
    {
        try
        {
            var info = new ProcessStartInfo { FileName = "xdg-open", UseShellExecute = false };
            info.ArgumentList.Add(item.Screenshot.Path);
            using var process = Process.Start(info);
        }
        catch (Exception ex)
        {
            _viewModel.ReportError(string.Format(
                CultureInfo.CurrentCulture, _viewModel.Strings["Screenshot.OpenFailed"], ex.Message));
        }
    }
}
