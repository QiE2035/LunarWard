using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward.Settings;

public partial class SettingAboutView : SettingSectionView
{
    public SettingAboutView(SettingAboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
