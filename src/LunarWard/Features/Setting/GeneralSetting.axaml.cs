using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Language;
using LunarWard.Services;
using LunarWard.Views.Starward;

namespace LunarWard.Features.Setting;

/// <summary>通用页（复刻 Starward <c>GeneralSetting.xaml</c>）。</summary>
/// <remarks>
/// 语言（<c>launcher.language</c>）与关闭行为（<c>launcher.closeWindowOption</c>）是真配置；
/// 主题是只读说明（LunarWard 只有暗色）。关闭到托盘由 Avalonia <c>TrayIcon</c> 实现，
/// 对应上游的 <c>SystemTrayWindow</c>。
/// </remarks>
public sealed partial class GeneralSetting : SettingPageBase
{
    private bool _initializing = true;


    /// <summary>
    /// 「最小化到系统托盘」（上游 <c>MainWindowCloseOption.Hide</c>）。三选一，未设置 = 每次询问。
    /// </summary>
    public bool IsCloseToTray
    {
        get => CurrentCloseWindowAction == CloseWindowAction.Hide;
        set
        {
            if (value)
            {
                SetCloseWindowOption(CloseWindowDecision.HideOption);
            }
        }
    }


    /// <summary>「完全退出」（上游 <c>MainWindowCloseOption.Exit</c>）。</summary>
    public bool IsCloseToExit
    {
        get => CurrentCloseWindowAction == CloseWindowAction.Exit;
        set
        {
            if (value)
            {
                SetCloseWindowOption(CloseWindowDecision.ExitOption);
            }
        }
    }


    /// <summary>
    /// 「每次关闭时询问」（上游配置为 0 / 未设置时的分支）：关闭时弹
    /// <see cref="Views.Starward.MainWindowCloseDialog"/>，选完写回配置。
    /// </summary>
    public bool IsAskEveryTime
    {
        get => CurrentCloseWindowAction == CloseWindowAction.Ask;
        set
        {
            if (value)
            {
                SetCloseWindowOption(CloseWindowDecision.AskOption);
            }
        }
    }


    private CloseWindowAction CurrentCloseWindowAction
        => CloseWindowDecision.Parse(Services.Config.Launcher.CloseWindowOption);


    /// <summary>写回关闭选项并通知三个单选按钮刷新（选中的那个把 IsChecked 置 true）。</summary>
    private void SetCloseWindowOption(string option)
    {
        if (string.Equals(Services.Config.Launcher.CloseWindowOption, option, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Services.Config.Launcher.CloseWindowOption = option;
        Services.Config.Save(Services.Paths.ConfigFile);
        OnPropertyChanged(nameof(IsCloseToTray));
        OnPropertyChanged(nameof(IsCloseToExit));
        OnPropertyChanged(nameof(IsAskEveryTime));
    }


    /// <summary>
    /// 开机自动启动（LunarWard 扩展）。上游 Windows 写注册表 <c>Run</c> 键；
    /// Linux 对应 <c>$XDG_CONFIG_HOME/autostart/lunarward.desktop</c>（带 <c>--start-hidden</c>）。
    /// </summary>
    public bool IsAutostartEnabled
    {
        get => LunarWard.Platform.AutostartEntry.IsEnabled(Services.Paths.ConfigHome);
        set
        {
            if (value == IsAutostartEnabled)
            {
                return;
            }

            LunarWard.Platform.AutostartEntry.SetEnabled(
                value, LunarWard.Platform.AutostartEntry.CurrentExecutable(), Services.Paths.ConfigHome);
            OnPropertyChanged();
            Status = string.Format(Strings[value ? "Settings.General.AutostartOn" : "Settings.General.AutostartOff"]);
        }
    }


    public GeneralSetting(PageContext context)
        : base(context)
    {
        InitializeComponent();
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
        DataContext = this;
    }


    /// <summary>可选语言（Starward 的 ComboBox 显示语言名而不是代码）。</summary>
    public IReadOnlyList<SettingLanguageOption> LanguageOptions { get; }


    private SettingLanguageOption _selectedLanguageOption;

    public SettingLanguageOption SelectedLanguageOption
    {
        get => _selectedLanguageOption;
        set
        {
            if (SetProperty(ref _selectedLanguageOption, value))
            {
                OnSelectedLanguageOptionChanged(value);
            }
        }
    }


    public string SelectedLanguage => SelectedLanguageOption?.Code ?? Strings.Language;


    private void OnSelectedLanguageOptionChanged(SettingLanguageOption value)
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

        // 设置页已迁到上游 Lang 资源，静态绑定不会自己刷新：换文化 + 广播消息，宿主页据此重建子页。
        Lang.Culture = System.Globalization.CultureInfo.GetCultureInfo(normalized);
        WeakReferenceMessenger.Default.Send(new LanguageChangedMessage());

        Status = string.Format(Strings["Settings.General.LanguageChanged"], normalized);
        OnPropertyChanged(nameof(IsEnglish));
    }


    public bool IsEnglish => SelectedLanguage == "en-US";


    private bool _announcements;

    public bool Announcements
    {
        get => _announcements;
        set
        {
            if (SetProperty(ref _announcements, value))
            {
                OnAnnouncementsChanged(value);
            }
        }
    }


    private void OnAnnouncementsChanged(bool value)
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


    private bool _videoBackground;

    public bool VideoBackground
    {
        get => _videoBackground;
        set
        {
            if (SetProperty(ref _videoBackground, value))
            {
                OnVideoBackgroundChanged(value);
            }
        }
    }


    private bool _enableGameAccountSwitcher;

    public bool EnableGameAccountSwitcher
    {
        get => _enableGameAccountSwitcher;
        set
        {
            if (SetProperty(ref _enableGameAccountSwitcher, value))
            {
                OnEnableGameAccountSwitcherChanged(value);
            }
        }
    }


    private void OnEnableGameAccountSwitcherChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        Services.Config.Launcher.EnableGameAccountSwitcher = value;
        SaveConfig();
    }


    private void OnVideoBackgroundChanged(bool value)
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
