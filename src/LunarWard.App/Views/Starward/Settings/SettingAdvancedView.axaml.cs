using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

public partial class SettingAdvancedView : SettingSectionView
{
    public SettingAdvancedView(SettingAdvancedViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
