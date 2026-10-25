using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>通用页（复刻 Starward <c>GeneralSetting.xaml</c>）。</summary>
/// <remarks>
/// 语言（<c>launcher.language</c>）与关闭行为（<c>launcher.closeWindowOption</c>）是真配置；
/// 主题是只读说明（LunarWard 只有暗色）。关闭到托盘由 Avalonia <c>TrayIcon</c> 实现，
/// 对应上游的 <c>SystemTrayWindow</c>。
/// </remarks>
public sealed partial class SettingGeneralViewModel : SettingSectionViewModel
{
    private bool _initializing = true;


    /// <summary>「最小化到系统托盘」为 true、「完全退出」为 false（映射上游 <c>CloseWindowOption</c>）。</summary>
    public bool IsCloseToTray
    {
        get => string.Equals(Services.Config.Launcher.CloseWindowOption, "Hide", StringComparison.OrdinalIgnoreCase);
        set
        {
            Services.Config.Launcher.CloseWindowOption = value ? "Hide" : "Exit";
            Services.Config.Save(Services.Paths.ConfigFile);
            OnPropertyChanged();
        }
    }


    public SettingGeneralViewModel(PageContext context, bool showDivider = false)
        : base(context, SettingSectionKind.General, "Settings.General", "\uE81E", showDivider)
    {
        LanguageOptions =
        [
            new SettingLanguageOption("zh-CN", "简体中文 (zh-CN)"),
            new SettingLanguageOption("en-US", "English (en-US)"),
        ];
        _selectedLanguageOption = LanguageOptions.First(o =>
            string.Equals(o.Code, Localization.AppStrings.Normalize(Services.Config.Launcher.Language), StringComparison.Ordinal));
        Announcements = true;
        VideoBackground = Services.Config.Launcher.VideoBackground;
        EnableGameAccountSwitcher = Services.Config.Launcher.EnableGameAccountSwitcher;
        _initializing = false;
    }


    /// <summary>可选语言（Starward 的 ComboBox 显示语言名而不是代码）。</summary>
    public IReadOnlyList<SettingLanguageOption> LanguageOptions { get; }


    [ObservableProperty]
    private SettingLanguageOption _selectedLanguageOption;


    public string SelectedLanguage => SelectedLanguageOption?.Code ?? Strings.Language;


    partial void OnSelectedLanguageOptionChanged(SettingLanguageOption value)
    {
        if (value is null)
        {
            return;
        }

        ApplyLanguage(value.Code);
        OnPropertyChanged(nameof(SelectedLanguage));
    }


    /// <summary>切换语言：立即生效并落盘（Starward 也是即时切换 + SaveConfiguration）。</summary>
    public void ApplyLanguage(string code)
    {
        var normalized = Localization.AppStrings.Normalize(code);
        var current = Localization.AppStrings.Normalize(Services.Config.Launcher.Language);
        Strings.Language = normalized;
        Services.Config.Launcher.Language = normalized;

        // ComboBox 首次绑定会把 SelectedItem 推回来一次；与当前配置相同就不当作"用户切换"，
        // 既不写盘也不弹状态（否则每次打开设置页都会白写一次 config.json）。
        if (_initializing || string.Equals(normalized, current, StringComparison.Ordinal))
        {
            return;
        }

        SaveConfig();
        Status = string.Format(Strings["Settings.General.LanguageChanged"], normalized);
        OnPropertyChanged(nameof(IsEnglish));
    }


    public bool IsEnglish => SelectedLanguage == "en-US";


    [ObservableProperty]
    private bool _announcements;


    partial void OnAnnouncementsChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        Services.Config.Launcher.ExtensionData ??= [];
        Services.Config.Launcher.ExtensionData["show_announcements"] =
            System.Text.Json.JsonSerializer.SerializeToElement(value);
        SaveConfig();
    }


    [ObservableProperty]
    private bool _videoBackground;


    [ObservableProperty]
    private bool _enableGameAccountSwitcher;


    partial void OnEnableGameAccountSwitcherChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        Services.Config.Launcher.EnableGameAccountSwitcher = value;
        SaveConfig();
    }


    partial void OnVideoBackgroundChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        Services.Config.Launcher.VideoBackground = value;
        SaveConfig();
    }


    private void SaveConfig()
    {
        try
        {
            Services.Config.Save(Services.Paths.ConfigFile);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }
}
