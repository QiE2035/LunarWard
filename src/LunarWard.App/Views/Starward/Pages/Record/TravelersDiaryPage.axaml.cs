using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.App.Views.Starward.Pages.Record;

/// <summary>
/// 旅行者札记页（Starward <c>Features/GameRecord/Genshin/TravelersDiaryPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱在选中「旅行者札记」且已有角色时创建。「获取明细」的月份 Flyout 在代码里按
/// Starward <c>MenuFlyout_GetDetails.Items</c> 的写法现场生成（前台 Flyout 内容不在
/// 逻辑树里，模板绑定拿不到页面 DataContext）。
/// </remarks>
public partial class TravelersDiaryPage : StarwardPage
{
    private readonly TravelersDiaryPageViewModel? _viewModel;


    public TravelersDiaryPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new TravelersDiaryPageViewModel(context, role);
        DataContext = _viewModel;
    }


    private void OnGetDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Control placementTarget)
        {
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var option in _viewModel.OptionalMonths)
        {
            flyout.Items.Add(new MenuItem
            {
                Header = option.Label,
                Command = _viewModel.FetchMonthCommand,
                CommandParameter = option,
            });
        }

        flyout.ShowAt(placementTarget);
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
    }
}
