using System;
using LunarWard.App.Localization;
using LunarWard.App.Services;
using LunarWard.App.Views.Starward.Controls;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// 主窗口关闭询问框（上游 <c>Features/ViewHost/MainWindowCloseDialog.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 上游在 <c>AppConfig.CloseWindowOption</c> 既不是 Hide 也不是 Exit（也就是 0 / 询问态）时弹出它，
/// 点「确定」后把选择写回配置，下次不再询问。LunarWard 的菜单/设置页额外交互见
/// <c>SettingGeneralView.axaml</c>（多了「每次关闭时询问」一项，对应上游 0 值）。
/// </para>
/// <para>
/// 关闭到托盘依赖 StatusNotifier 宿主；宿主不存在时 <c>Hide</c> 这一项没有意义，
/// 这里置灰并把默认选项收敛到「完全退出」，避免「选了隐藏、窗口却再也回不来」。
/// </para>
/// </remarks>
public partial class MainWindowCloseDialog : StarwardContentDialog
{
    /// <summary>设计器/编译用无参构造。</summary>
    public MainWindowCloseDialog()
        : this(trayAvailable: true, currentOption: null)
    {
    }


    /// <param name="trayAvailable">桌面是否提供系统托盘（决定「最小化到系统托盘」是否可选）。</param>
    /// <param name="currentOption">当前配置值，用于预选。</param>
    public MainWindowCloseDialog(bool trayAvailable, string? currentOption)
    {
        InitializeComponent();

        var strings = AppStrings.Instance;
        Title = strings["Settings.General.CloseWindowOption"];
        PrimaryButtonText = strings["Common.Confirm"];
        CloseButtonText = strings["Common.Cancel"];

        HintText.Text = strings["Settings.General.CloseDialogHint"];
        HideRadio.Content = strings["Settings.General.MinimizeToTray"];
        ExitRadio.Content = strings["Settings.General.ExitCompletely"];
        HideRadio.IsEnabled = trayAvailable;

        NoTrayHintText.Text = strings["Settings.General.NoTrayHint"];
        NoTrayHintText.IsVisible = !trayAvailable;

        bool preferHide = trayAvailable && CloseWindowDecision.Parse(currentOption) != CloseWindowAction.Exit;
        HideRadio.IsChecked = preferHide;
        ExitRadio.IsChecked = !preferHide;

        PrimaryButtonClicked += (_, _) => Confirmed = true;
    }


    /// <summary>用户点了「确定」（false = 取消/ESC/X，保持原配置）。</summary>
    public bool Confirmed { get; private set; }


    /// <summary>用户选择的关闭行为（<see cref="CloseWindowDecision.HideOption"/> / <see cref="CloseWindowDecision.ExitOption"/>）。</summary>
    public string CloseWindowOption
        => HideRadio.IsChecked == true && HideRadio.IsEnabled
            ? CloseWindowDecision.HideOption
            : CloseWindowDecision.ExitOption;
}
