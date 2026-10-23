using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

public partial class SettingAboutView : SettingSectionView
{
    public SettingAboutView(SettingAboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
