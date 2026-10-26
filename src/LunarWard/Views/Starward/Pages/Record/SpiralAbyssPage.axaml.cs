using LunarWard.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.Views.Starward.Pages.Record;

/// <summary>
/// 深境螺旋页（Starward <c>Features/GameRecord/Genshin/SpiralAbyssPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱（<see cref="ToolboxPage"/>）在选中「深境螺旋」且已有角色时创建，
/// 角色从工具箱左栏的登录态来（<see cref="ToolboxPageViewModel.CurrentRole"/>）。
/// </remarks>
public partial class SpiralAbyssPage : StarwardPage
{
    private readonly SpiralAbyssPageViewModel? _viewModel;


    public SpiralAbyssPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new SpiralAbyssPageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
    }
}
