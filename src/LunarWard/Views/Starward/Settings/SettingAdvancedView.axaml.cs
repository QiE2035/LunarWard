using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward.Settings;

public partial class SettingAdvancedView : SettingSectionView
{
    public SettingAdvancedView(SettingAdvancedViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
