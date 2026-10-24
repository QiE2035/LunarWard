using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.App.Views.Starward.Pages.Record;

/// <summary>
/// 绳网月报页（Starward <c>Features/GameRecord/ZZZ/InterKnotMonthlyReportPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱在选中「绳网月报」且已有角色时创建；角色来自工具箱左栏登录态。
/// 「获取明细」按钮按 <see cref="InterKnotMonthlyReportPageViewModel.OptionalMonths"/>
/// 构建月份 Flyout（Starward <c>Button_GetDetails</c>）。
/// </remarks>
public partial class InterKnotMonthlyReportPage : StarwardPage
{
    private InterKnotMonthlyReportPageViewModel? _viewModel;


    public InterKnotMonthlyReportPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new InterKnotMonthlyReportPageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }


    /// <summary>Starward Button_GetDetails：按可选月份构建 MenuFlyout。</summary>
    private void OnGetDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Control button)
        {
            return;
        }

        var flyout = new MenuFlyout
        {
            Placement = PlacementMode.RightEdgeAlignedTop,
        };
        foreach (var month in _viewModel.OptionalMonths)
        {
            var item = new MenuItem
            {
                Header = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0:MMM}",
                    DateTime.ParseExact(month, "yyyyMM", CultureInfo.InvariantCulture)),
            };
            item.Click += async (_, _) => await _viewModel.LoadMonthDetailsAsync(month);
            flyout.Items.Add(item);
        }

        if (flyout.Items.Count > 0)
        {
            flyout.ShowAt(button);
        }
    }
}
