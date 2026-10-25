using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.App.Views.Starward.Pages.Record;

/// <summary>
/// 忘却之庭页（Starward <c>Features/GameRecord/StarRail/ForgottenHallPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱在选中「忘却之庭」且已有角色时创建；页面本身没有代码独有的交互
/// （刷新、选期都是命令绑定），逻辑全部在 <see cref="ForgottenHallPageViewModel"/>。
/// </remarks>
public partial class ForgottenHallPage : StarwardPage
{
    private readonly ForgottenHallPageViewModel? _viewModel;


    public ForgottenHallPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new ForgottenHallPageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
    }
}
