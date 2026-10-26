using LunarWard.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.Views.Starward.Pages.Record;

/// <summary>
/// 异象仲裁页（Starward <c>Features/GameRecord/StarRail/ChallengePeakPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱在选中「异象仲裁」且已有角色时创建；刷新与选期都是命令/绑定，
/// 逻辑全部在 <see cref="ChallengePeakPageViewModel"/>。
/// </remarks>
public partial class ChallengePeakPage : StarwardPage
{
    private readonly ChallengePeakPageViewModel? _viewModel;


    public ChallengePeakPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new ChallengePeakPageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
    }
}
