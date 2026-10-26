using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Views.Starward;
using LunarWard.Launch;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 工具箱页（复刻 Starward <c>ToolboxSetting.xaml</c> 的 180×220 卡片网格）。
/// </summary>
/// <remarks>
/// Starward 的卡片是「每日便笺 / 账号记录 / 抽卡记录 …」的跳转入口；LunarWard 这里
/// 放的是本项目的工具箱入口（游戏设置 / 诊断 / 环境 shell / 截图），跳转统一走
/// <see cref="ShellViewModel.Navigate"/>，不另开一套导航。
/// </remarks>
public sealed partial class SettingToolboxViewModel : SettingSectionViewModel
{
    public SettingToolboxViewModel(PageContext context, bool showDivider = false)
        : base(context, SettingSectionKind.Toolbox, "Settings.Toolbox", "\uEC7A", showDivider)
    {
        Items =
        [
            new SettingToolboxItemViewModel(
                "\uE7FC",
                Strings["Settings.Toolbox.GameSetting"],
                Strings["Settings.Toolbox.GameSettingDesc"],
                () => Shell.Navigate(ShellPage.GameSetting)),
            new SettingToolboxItemViewModel(
                "\uE9D9",
                Strings["Settings.Toolbox.Diagnose"],
                Strings["Settings.Toolbox.DiagnoseDesc"],
                OpenDiagnostics),
            new SettingToolboxItemViewModel(
                "\uE756",
                Strings["Settings.Toolbox.Shell"],
                Strings["Settings.Toolbox.ShellDesc"],
                OpenEnvironmentShell),
            new SettingToolboxItemViewModel(
                "\uE91B",
                Strings["Settings.Toolbox.Screenshot"],
                Strings["Settings.Toolbox.ScreenshotDesc"],
                () => Shell.Navigate(ShellPage.Screenshot)),
        ];
    }


    public IReadOnlyList<SettingToolboxItemViewModel> Items { get; }


    [RelayCommand]
    private void Invoke(SettingToolboxItemViewModel? item) => item?.Invoke();


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


/// <summary>工具箱卡片（Starward <c>ToolboxItem</c>）：图标 + 名称 + 说明 + 动作。</summary>
public sealed class SettingToolboxItemViewModel
{
    private readonly Action _action;


    public SettingToolboxItemViewModel(string icon, string name, string description, Action action)
    {
        Icon = icon;
        Name = name;
        Description = description;
        _action = action;
    }


    public string Icon { get; }

    public string Name { get; }

    public string Description { get; }

    public void Invoke() => _action();
}
