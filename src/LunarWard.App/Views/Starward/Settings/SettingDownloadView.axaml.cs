using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Settings;

public partial class SettingDownloadView : SettingSectionView
{
    public SettingDownloadView(SettingDownloadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
