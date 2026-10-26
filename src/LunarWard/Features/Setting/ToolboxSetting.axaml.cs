using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Views.Starward;
using LunarWard.Launch;

namespace LunarWard.Features.Setting;

/// <summary>
/// 工具箱页（复刻 Starward <c>ToolboxSetting.xaml</c> 的 180×220 卡片网格）。
/// </summary>
/// <remarks>
/// Starward 的卡片是「每日便笺 / 账号记录 / 抽卡记录 …」的跳转入口；LunarWard 这里
/// 放的是本项目的工具箱入口（游戏设置 / 诊断 / 环境 shell / 截图），跳转统一走
/// <see cref="ShellViewModel.Navigate"/>，不另开一套导航。
/// </remarks>
public sealed partial class ToolboxSetting : SettingPageBase
{
    public ToolboxSetting(PageContext context)
        : base(context)
    {
        InitializeComponent();
        Items =
        [
            new ToolboxItem(
                "\uE7FC",
                Strings["Settings.Toolbox.GameSetting"],
                Strings["Settings.Toolbox.GameSettingDesc"],
                () => Shell.Navigate(ShellPage.GameSetting)),
            new ToolboxItem(
                "\uE9D9",
                Strings["Settings.Toolbox.Diagnose"],
                Strings["Settings.Toolbox.DiagnoseDesc"],
                OpenDiagnostics),
            new ToolboxItem(
                "\uE756",
                Strings["Settings.Toolbox.Shell"],
                Strings["Settings.Toolbox.ShellDesc"],
                OpenEnvironmentShell),
            new ToolboxItem(
                "\uE91B",
                Strings["Settings.Toolbox.Screenshot"],
                Strings["Settings.Toolbox.ScreenshotDesc"],
                () => Shell.Navigate(ShellPage.Screenshot)),
        ];
        DataContext = this;
    }


    public IReadOnlyList<ToolboxItem> Items { get; }


    [RelayCommand]
    private void Invoke(ToolboxItem? item) => item?.Invoke();


    private void OpenDiagnostics()
    {
        var report = LaunchReport.ReadFrom(Services.Paths.LaunchReportFile);
        if (report is null)
        {
            Status = Strings["Settings.Toolbox.NoReport"];
            return;
        }

        var window = new Views.DiagnosticWindow
        {
            DataContext = new ViewModels.DiagnosticViewModel(report, Services.Paths),
        };
        window.Show();
        Status = null;
    }


    private void OpenEnvironmentShell()
    {
        try
        {
            var plan = Services.LauncherService.BuildPlan(Profile.Id, Profile.Profile, dryRun: true);
            var window = new Views.Starward.Legacy.ShellWindow
            {
                DataContext = new ViewModels.EnvironmentShellViewModel(plan, Services.Paths),
            };

            window.Show();
            Status = null;
        }
        catch (Exception ex)
        {
            Status = $"{Strings["Settings.Toolbox.NeedProfile"]}（{ex.Message}）";
        }
    }
}
