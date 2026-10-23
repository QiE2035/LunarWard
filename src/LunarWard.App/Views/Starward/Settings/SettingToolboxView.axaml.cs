using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

public partial class SettingToolboxView : SettingSectionView
{
    public SettingToolboxView(SettingToolboxViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
