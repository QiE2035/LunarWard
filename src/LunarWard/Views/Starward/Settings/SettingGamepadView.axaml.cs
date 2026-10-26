using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward.Settings;

public partial class SettingGamepadView : SettingSectionView
{
    public SettingGamepadView(SettingGamepadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
