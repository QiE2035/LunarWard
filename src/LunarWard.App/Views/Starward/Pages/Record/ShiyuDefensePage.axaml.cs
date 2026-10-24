using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.App.Views.Starward.Pages.Record;

/// <summary>
/// 式舆防卫战页（Starward <c>Features/GameRecord/ZZZ/ShiyuDefensePage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱（<see cref="ToolboxPage"/>）在选中「式舆防卫战」且已有角色时创建，
/// 进入即由 VM 拉取「当期 / 上期」两个赛期。
/// </remarks>
public partial class ShiyuDefensePage : StarwardPage
{
    private ShiyuDefensePageViewModel? _viewModel;


    public ShiyuDefensePage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new ShiyuDefensePageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
