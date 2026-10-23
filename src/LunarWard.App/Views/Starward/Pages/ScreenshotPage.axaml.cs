using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

public partial class ScreenshotPage : StarwardPage
{
    private readonly ScreenshotPageViewModel _viewModel;


    public ScreenshotPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new ScreenshotPageViewModel(context);
        DataContext = _viewModel;
    }


    public override void OnNavigatedTo()
    {
        // 每次进入页面都重新扫目录：玩家可能刚在游戏里截了新图
        _viewModel.ReloadFromDisk();
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel.Dispose();
    }
}
