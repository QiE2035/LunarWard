using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

public partial class SettingFileManageView : SettingSectionView
{
    public SettingFileManageView(SettingFileManageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
