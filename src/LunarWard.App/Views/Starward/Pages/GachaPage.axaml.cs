using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
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
        _viewModel = new GachaPageViewModel(context);
        DataContext = _viewModel;

        _viewModel.Pools.CollectionChanged += OnPoolsChanged;
        SizeChanged += OnSizeChanged;
        QueueStatsCardLayout();
    }


    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => QueueStatsCardLayout();


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
