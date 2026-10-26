using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward.Settings;

public partial class SettingGeneralView : SettingSectionView
{
    public SettingGeneralView(SettingGeneralViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
