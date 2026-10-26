using LunarWard.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.Views.Starward.Pages.Record;

/// <summary>
/// 危局强袭战页（Starward <c>Features/GameRecord/ZZZ/DeadlyAssaultPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱（<see cref="ToolboxPage"/>）在选中「危局强袭战」且已有角色时创建，
/// 进入即由 VM 拉取「当期 / 上期」两个赛期。
/// </remarks>
public partial class DeadlyAssaultPage : StarwardPage
{
    private DeadlyAssaultPageViewModel? _viewModel;


    public DeadlyAssaultPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new DeadlyAssaultPageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
