using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

public partial class SettingGamepadView : SettingSectionView
{
    public SettingGamepadView(SettingGamepadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
