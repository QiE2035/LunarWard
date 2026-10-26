using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward.Settings;

public partial class SettingScreenshotView : SettingSectionView
{
    public SettingScreenshotView(SettingScreenshotViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
