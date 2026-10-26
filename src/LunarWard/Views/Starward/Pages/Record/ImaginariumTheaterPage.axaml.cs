using LunarWard.ViewModels.Starward;
using LunarWard.Core.GameRecord;

namespace LunarWard.Views.Starward.Pages.Record;

/// <summary>
/// 幻想真境剧诗页（Starward <c>Features/GameRecord/Genshin/ImaginariumTheaterPage.xaml</c>）。
/// </summary>
/// <remarks>
/// 由工具箱（<see cref="ToolboxPage"/>）在选中「幻想真境剧诗」且已有角色时创建。
/// </remarks>
public partial class ImaginariumTheaterPage : StarwardPage
{
    private readonly ImaginariumTheaterPageViewModel? _viewModel;


    public ImaginariumTheaterPage(PageContext context, GameRecordRole role) : base(context)
    {
        InitializeComponent();
        _viewModel = new ImaginariumTheaterPageViewModel(context, role);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
    }
}
