using LunarWard.ViewModels.Starward;

namespace LunarWard.Views.Starward.Settings;

public partial class SettingDownloadView : SettingSectionView
{
    public SettingDownloadView(SettingDownloadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
