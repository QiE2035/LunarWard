using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

public partial class SettingScreenshotView : SettingSectionView
{
    public SettingScreenshotView(SettingScreenshotViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
