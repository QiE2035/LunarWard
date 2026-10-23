using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

public partial class GameSettingPage : StarwardPage
{
    private GameSettingPageViewModel? _viewModel;

    public GameSettingPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new GameSettingPageViewModel(context);
        DataContext = _viewModel;
    }

    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }
}
