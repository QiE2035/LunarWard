using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

public partial class SettingGeneralView : SettingSectionView
{
    public SettingGeneralView(SettingGeneralViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
