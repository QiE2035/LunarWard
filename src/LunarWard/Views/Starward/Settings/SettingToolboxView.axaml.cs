using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward.Settings;

public partial class SettingToolboxView : SettingSectionView
{
    public SettingToolboxView(SettingToolboxViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
