using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.App.Views.Starward.Pages.Record;

/// <summary>
/// 末日幻影页（Starward <c>Features/GameRecord/StarRail/ApocalypticShadowPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱在选中「末日幻影」且已有角色时创建；刷新、选期都是命令绑定，
/// 逻辑全部在 <see cref="ApocalypticShadowPageViewModel"/>。
/// </remarks>
public partial class ApocalypticShadowPage : StarwardPage
{
    private readonly ApocalypticShadowPageViewModel? _viewModel;


    public ApocalypticShadowPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new ApocalypticShadowPageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
    }
}
