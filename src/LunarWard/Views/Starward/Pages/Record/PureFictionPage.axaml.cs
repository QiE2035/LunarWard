using LunarWard.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.Views.Starward.Pages.Record;

/// <summary>
/// 虚构叙事页（Starward <c>Features/GameRecord/StarRail/PureFictionPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱在选中「虚构叙事」且已有角色时创建；页面没有代码独有的交互
/// （刷新、选期都是命令绑定），逻辑全部在 <see cref="PureFictionPageViewModel"/>。
/// </remarks>
public partial class PureFictionPage : StarwardPage
{
    private readonly PureFictionPageViewModel? _viewModel;


    public PureFictionPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new PureFictionPageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
    }
}
