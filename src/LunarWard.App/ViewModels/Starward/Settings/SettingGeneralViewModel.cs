using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>通用页（复刻 Starward <c>GeneralSetting.xaml</c>）。</summary>
/// <remarks>
/// 语言是真配置（<c>launcher.language</c>）；主题与关闭行为是只读说明
/// （LunarWard 只有暗色、只有直接退出）。
/// </remarks>
public sealed partial class SettingGeneralViewModel : SettingSectionViewModel
{
    private bool _initializing = true;


    public SettingGeneralViewModel(PageContext context, bool showDivider = false)
        : base(context, SettingSectionKind.General, "Settings.General", "\uE81E", showDivider)
    {
        Languages = Strings.SupportedLanguages.ToList();
        SelectedLanguage = Localization.AppStrings.Normalize(Services.Config.Launcher.Language);
        OnPropertyChanged(nameof(LanguageDisplay));
        Announcements = true;
        VideoBackground = Services.Config.Launcher.VideoBackground;
        _initializing = false;
    }


    /// <summary>可选语言（zh-CN / en-US）。</summary>
    public IReadOnlyList<string> Languages { get; }


    [ObservableProperty]
    private string _selectedLanguage;


    partial void OnSelectedLanguageChanged(string value)
    {
        var normalized = Localization.AppStrings.Normalize(value);
        Strings.Language = normalized;
        Services.Config.Launcher.Language = normalized;
        if (!_initializing)
        {
            SaveConfig();
        }
        Status = _initializing ? null : string.Format(Strings["Settings.General.LanguageChanged"], normalized);
        OnPropertyChanged(nameof(IsEnglish));
    }


    /// <summary>语言显示名（ComboBox 里显示 "简体中文 (zh-CN)"）。</summary>
    public bool IsEnglish => SelectedLanguage == "en-US";


    public string LanguageDisplay => IsEnglish ? "English (en-US)" : "简体中文 (zh-CN)";


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


    partial void OnVideoBackgroundChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        Services.Config.Launcher.VideoBackground = value;
        SaveConfig();
    }


    [ObservableProperty]
    private string? _status;


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
