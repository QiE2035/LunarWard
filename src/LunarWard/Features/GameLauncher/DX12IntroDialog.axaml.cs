using CommunityToolkit.Mvvm.Input;
using LunarWard.Core.HoYoPlay;
using LunarWard.Frameworks.Dialogs;

namespace LunarWard.Features.GameLauncher;

/// <summary>
/// DX12 画质预览（首次切换 DX12 时的说明弹窗）。<b>移植自</b>
/// <c>Starward/src/Starward/Features/GameLauncher/DX12IntroDialog.xaml(.cs)</c>。
/// </summary>
public sealed partial class DX12IntroDialog : StarwardContentDialog
{


    public GameDXConfig GameDXConfig { get; set; }



    public DX12IntroDialog()
    {
        this.InitializeComponent();
    }



    [RelayCommand]
    private void CloseDialog()
    {
        this.Close();
    }


}
