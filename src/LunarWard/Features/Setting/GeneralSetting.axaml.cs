using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Language;
using LunarWard.Frameworks;
using LunarWard.Features.ViewHost;
using LunarWard.Services; // CloseWindowAction（关闭行为的三态，独立于 AppServices）
using System.Globalization;

namespace LunarWard.Features.Setting;

/// <summary>通用页（复刻 Starward <c>GeneralSetting.xaml</c>）。</summary>
/// <remarks>
/// 语言（<c>launcher.language</c>）与关闭行为（<c>launcher.closeWindowOption</c>）是真配置；
/// 主题是只读说明（LunarWard 只有暗色）。关闭到托盘由 Avalonia <c>TrayIcon</c> 实现，
/// 对应上游的 <c>SystemTrayWindow</c>。
/// </remarks>
public sealed partial class GeneralSetting : PageBase
{

    public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    /// <summary>错误/状态行（上游各设置页末尾的 InfoBar 文本）。</summary>
    public string? Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    private string? _status;

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
                SetCloseWindowOption(CloseWindowAction.Hide);
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
                SetCloseWindowOption(CloseWindowAction.Exit);
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
                SetCloseWindowOption(CloseWindowAction.Ask);
            }
        }
    }


    private CloseWindowAction CurrentCloseWindowAction
        => AppConfig.CloseWindowOption switch
        {
            MainWindowCloseOption.Hide => CloseWindowAction.Hide,
            MainWindowCloseOption.Exit => CloseWindowAction.Exit,
            // 0 / 未设置 = 每次询问（上游默认值）
            _ => CloseWindowAction.Ask,
        };


    /// <summary>写回关闭选项并通知三个单选按钮刷新（选中的那个把 IsChecked 置 true）。</summary>
    private void SetCloseWindowOption(CloseWindowAction action)
    {
        var option = action switch
        {
            CloseWindowAction.Hide => MainWindowCloseOption.Hide,
            CloseWindowAction.Exit => MainWindowCloseOption.Exit,
            _ => (MainWindowCloseOption)0,
        };

        if (AppConfig.CloseWindowOption == option)
        {
            return;
        }

        AppConfig.CloseWindowOption = option;
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
        get => LunarWard.Platform.AutostartEntry.IsEnabled(LunarWard.Platform.XdgPaths.Default.ConfigHome);
        set
        {
            if (value == IsAutostartEnabled)
            {
                return;
            }

            LunarWard.Platform.AutostartEntry.SetEnabled(
                value, LunarWard.Platform.AutostartEntry.CurrentExecutable(), LunarWard.Platform.XdgPaths.Default.ConfigHome);
            OnPropertyChanged();
            // LunarWard 扩展文案：上游没有开机自启开关，内联即可（见 docs/09 §十一）。
            Status = value ? "已随登录启动" : "未随登录启动";
        }
    }


    public GeneralSetting()
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();
        LanguageOptions =
        [
            new SettingLanguageOption("zh-CN", "简体中文 (zh-CN)"),
            new SettingLanguageOption("en-US", "English (en-US)"),
        ];
        _selectedLanguageOption = LanguageOptions.FirstOrDefault(o =>
            string.Equals(o.Code, SettingLocalization.Normalize(AppConfig.Language), StringComparison.Ordinal))
            ?? LanguageOptions[0];
        Announcements = AppConfig.ShowAnnouncements;
        VideoBackground = AppConfig.VideoBackground;
        EnableGameAccountSwitcher = AppConfig.EnableGameAccountSwitcher;
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


    public string SelectedLanguage => SelectedLanguageOption?.Code ?? SettingLocalization.Normalize(AppConfig.Language);


    private void OnSelectedLanguageOptionChanged(SettingLanguageOption value)
    {
        if (value is null)
        {
            return;
        }

        ApplyLanguage(value.Code);
        OnPropertyChanged(nameof(SelectedLanguage));
    }


    /// <summary>切换语言：立即生效（上游同样即时切换；配置写 <c>Setting</c> 表，无需显式 SaveConfiguration）。</summary>
    public void ApplyLanguage(string code)
    {
        var normalized = SettingLocalization.Normalize(code);
        var current = SettingLocalization.Normalize(AppConfig.Language);
        AppConfig.SetLanguage(normalized);

        // ComboBox 首次绑定会把 SelectedItem 推回来一次；与当前配置相同就不当作"用户切换"，
        // 既不弹状态也不广播消息（否则每次打开设置页都会白走一次）。
        if (_initializing || string.Equals(normalized, current, StringComparison.Ordinal))
        {
            return;
        }

        // 设置页已迁到上游 Lang 资源，静态绑定不会自己刷新：换文化 + 广播消息，宿主页据此重建子页。
        Lang.Culture = CultureInfo.GetCultureInfo(normalized);
        // 上游同样在切换后 SaveConfiguration()：语言写在 config.ini 的 Language= 一行。
        AppConfig.SaveConfiguration();
        WeakReferenceMessenger.Default.Send(new LanguageChangedMessage());

        Status = string.Format("语言已切换为 {0}", normalized);
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

        AppConfig.ShowAnnouncements = value;
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

        AppConfig.EnableGameAccountSwitcher = value;
    }


    private void OnVideoBackgroundChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        AppConfig.VideoBackground = value;
    }


}
