using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.App.Views.Starward.Pages.Record;

/// <summary>
/// 幽境危战页（Starward <c>Features/GameRecord/Genshin/StygianOnslaughtPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱（<see cref="ToolboxPage"/>）在选中「幽境危战」且已有角色时创建，
/// 角色从工具箱左栏的登录态来（<see cref="ToolboxPageViewModel.CurrentRole"/>）。
/// </remarks>
public partial class StygianOnslaughtPage : StarwardPage
{
    private readonly StygianOnslaughtPageViewModel? _viewModel;


    public StygianOnslaughtPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new StygianOnslaughtPageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
    }
}
