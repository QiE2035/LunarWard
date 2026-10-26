using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward.Settings;

public partial class SettingHotkeyView : SettingSectionView
{
    public SettingHotkeyView(SettingHotkeyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
