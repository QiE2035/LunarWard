using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

public partial class SettingHotkeyView : SettingSectionView
{
    public SettingHotkeyView(SettingHotkeyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
