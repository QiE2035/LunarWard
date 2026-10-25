using LunarWard.App.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Localization;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>设置页左侧二级导航的条目（顺序与 Starward SettingPage 一致）。</summary>
public enum SettingSectionKind
{
    About,
    General,
    Download,
    FileManage,
    Screenshot,
    Hotkey,
    Gamepad,
    Advanced,
    Toolbox,
}


/// <summary>
/// 设置子页 tag 解析（<c>--settings-section</c> 自检开关与 <c>SettingPage.SelectSection</c> 共用）。
/// </summary>
/// <remarks>
/// tag 用 Starward 页面名：<c>about</c> / <c>general</c> / <c>download</c> / <c>files</c> /
/// <c>screenshot</c> / <c>hotkey</c> / <c>gamepad</c> / <c>advanced</c> / <c>toolbox</c>；
/// 同时接受 <c>SettingPage.xaml</c> 里的完整 Tag（如 <c>FileManageSetting</c>）。
/// </remarks>
internal static class SettingSectionTags
{
    public static SettingSectionKind? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var text = tag.Trim().Replace("-", "").Replace("_", "");
        if (text.EndsWith("Setting", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^"Setting".Length];
        }

        return text.ToLowerInvariant() switch
        {
            "about" => SettingSectionKind.About,
            "general" => SettingSectionKind.General,
            "download" => SettingSectionKind.Download,
            "files" or "filemanage" or "filemanagement" => SettingSectionKind.FileManage,
            "screenshot" or "screenshots" => SettingSectionKind.Screenshot,
            "hotkey" or "hotkeys" or "keyboardshortcuts" => SettingSectionKind.Hotkey,
            "gamepad" or "gamepadcontrol" => SettingSectionKind.Gamepad,
            "advanced" => SettingSectionKind.Advanced,
            "toolbox" => SettingSectionKind.Toolbox,
            _ => null,
        };
    }
}


/// <summary>
/// 设置子页 VM 的基类：子页都是 <see cref="StarwardPageViewModel"/>（拿到同一份
/// <see cref="StarwardPageViewModel.Context"/>），并负责自己的标题本地化。
/// </summary>
public abstract partial class SettingSectionViewModel : StarwardPageViewModel
{
    protected SettingSectionViewModel(
        PageContext context,
        SettingSectionKind kind,
        string titleKey,
        string icon,
        bool showDivider = false)
        : base(context)
    {
        Kind = kind;
        TitleKey = titleKey;
        Icon = icon;
        ShowDivider = showDivider;
        Strings.PropertyChanged += OnStringsChanged;
    }


    public SettingSectionKind Kind { get; }

    /// <summary>导航项文案键（语言切换后由 <see cref="Title"/> 重新取）。</summary>
    public string TitleKey { get; }

    /// <summary>Segoe MDL2 图标（与 Starward 的 FontIcon Glyph 对应）。</summary>
    public string Icon { get; }

    /// <summary>该导航项上方是否画一条分隔线（Starward 的 NavigationViewItemSeparator）。</summary>
    public bool ShowDivider { get; }

    public string Title => Strings[TitleKey];


    /// <summary>子页底部的状态/错误文字（各子页共用同一个字段）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _status;


    public bool HasStatus => !string.IsNullOrEmpty(Status);


    private void OnStringsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => OnPropertyChanged(nameof(Title));


    public override void Dispose()
    {
        Strings.PropertyChanged -= OnStringsChanged;
    }
}


/// <summary>
/// 设置页（复刻 Starward <c>Features/Setting/SettingPage.xaml</c>）：
/// 左侧 260px 二级导航 + 右侧 <c>Frame Margin="24,24,0,0"</c>。
/// </summary>
/// <remarks>
/// <para>
/// Avalonia 没有 <c>NavigationView</c>，所以用 <c>Grid ColumnDefinitions="260,*"</c> +
/// <c>ListBox</c> 复刻：每项 40 高、图标 16 + 文字 14、选中态左侧 3×16 强调条 +
/// <c>#14FFFFFF</c> 底（<c>StarSelectionBrush</c>）。
/// </para>
/// <para>
/// 子页实例由 View 缓存（<c>ContentControl</c> + 字典），VM 只负责"当前选哪一页"。
/// </para>
/// </remarks>
public sealed partial class SettingPageViewModel : StarwardPageViewModel
{
    public SettingPageViewModel(PageContext context)
        : base(context)
    {
        Confirmation = new SettingConfirmationViewModel(Strings);

        Sections =
        [
            new SettingAboutViewModel(context),
            new SettingGeneralViewModel(context),
            new SettingDownloadViewModel(context),
            new SettingFileManageViewModel(context, Confirmation.ShowAsync),
            new SettingScreenshotViewModel(context),
            new SettingHotkeyViewModel(context),
            new SettingGamepadViewModel(context),
            new SettingAdvancedViewModel(context),
            // Starward 在「高级」与「工具箱」之间有一条分隔线
            new SettingToolboxViewModel(context, showDivider: true),
        ];

        _selectedSection = ResolveInitialSection() ?? Sections[0];
    }


    /// <summary>
    /// 初始子页。默认「关于」（与 Starward <c>Frame_Setting.Navigate(typeof(AboutSetting))</c> 一致）；
    /// 自检/截图时可用 <c>--settings-section &lt;tag&gt;</c>（旧开关
    /// <c>LUNARWARD_SETTINGS_SECTION</c> 仍保留）直接落到某一页。
    /// </summary>
    private SettingSectionViewModel? ResolveInitialSection()
    {
        var requested = CaptureMode.SettingsSection;
        if (string.IsNullOrWhiteSpace(requested))
        {
            requested = Environment.GetEnvironmentVariable("LUNARWARD_SETTINGS_SECTION");
        }

        var kind = SettingSectionTags.Parse(requested);
        return kind is null ? null : Sections.FirstOrDefault(s => s.Kind == kind);
    }


    /// <summary>二级导航条目。</summary>
    public ObservableCollection<SettingSectionViewModel> Sections { get; }

    /// <summary>页内二次确认对话框（缓存清理用）。</summary>
    public SettingConfirmationViewModel Confirmation { get; }


    [ObservableProperty]
    private SettingSectionViewModel _selectedSection;


    public override void Dispose()
    {
        foreach (var section in Sections)
        {
            section.Dispose();
        }

        base.Dispose();
    }
}


/// <summary>
/// 页内模态确认框（等价于 Starward 的 <c>ContentDialog</c>）。
/// </summary>
/// <remarks>
/// 设置页没有独立窗口可开，也不引入新的 DialogHost：用页面最上层的一个遮罩层实现，
/// 由 <see cref="ShowAsync"/> 返回用户的选择。
/// </remarks>
public sealed partial class SettingConfirmationViewModel : ObservableObject
{
    private readonly AppStrings _strings;

    private TaskCompletionSource<bool>? _completion;


    public SettingConfirmationViewModel(AppStrings strings) => _strings = strings;


    [ObservableProperty]
    private bool _isOpen;


    [ObservableProperty]
    private string _title = "";


    [ObservableProperty]
    private string _body = "";


    [ObservableProperty]
    private string _confirmText = "";


    [ObservableProperty]
    private string _cancelText = "";


    /// <summary>显示确认框，等待用户点「确定」/「取消」。</summary>
    public Task<bool> ShowAsync(string title, string body)
    {
        // 连续两次请求时，把前一个请求当成取消（避免悬挂的 Task）
        _completion?.TrySetResult(false);

        _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Title = title;
        Body = body;
        ConfirmText = _strings["Settings.Common.Confirm"];
        CancelText = _strings["Settings.Common.Cancel"];
        IsOpen = true;
        return _completion.Task;
    }


    [RelayCommand]
    private void Confirm() => Complete(true);


    [RelayCommand]
    private void Cancel() => Complete(false);


    private void Complete(bool result)
    {
        IsOpen = false;
        var completion = _completion;
        _completion = null;
        completion?.TrySetResult(result);
    }
}
