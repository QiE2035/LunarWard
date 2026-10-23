using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>关于页（复刻 Starward <c>AboutSetting.xaml</c>）。</summary>
/// <remarks>
/// Starward 有"检查更新 / 预览版通道"；LunarWard 走发行包升级，不内置更新器，
/// 因此这里只保留版本、许可、链接、致谢与日志目录。
/// </remarks>
public sealed partial class SettingAboutViewModel : SettingSectionViewModel
{
    public SettingAboutViewModel(PageContext context, bool showDivider = false)
        : base(context, SettingSectionKind.About, "Settings.About", "\uE946", showDivider)
    {
        Version = ReadVersion();
        LogDirectory = Services.Paths.LogDirectory;

        Links = new List<SettingLinkViewModel>
        {
            new SettingLinkViewModel(Strings["Settings.About.Homepage"], "https://github.com/QiE2035/LunarWard"),
            new SettingLinkViewModel(Strings["Settings.About.Upstream"], "https://github.com/Scighost/Starward"),
            new SettingLinkViewModel(Strings["Settings.About.Issues"], "https://github.com/QiE2035/LunarWard/issues"),
            new SettingLinkViewModel(Strings["Settings.About.Docs"], "https://github.com/QiE2035/LunarWard/blob/main/docs/07-UI-REPLICA.md"),
        };
    }


    /// <summary>应用名（Starward 里是 24px Bold 的「Starward」）。</summary>
    public string AppName => "LunarWard";

    public string Version { get; }

    public string LogDirectory { get; }

    public List<SettingLinkViewModel> Links { get; }


    [RelayCommand]
    private async Task OpenLinkAsync(SettingLinkViewModel? link)
    {
        if (link is null)
        {
            return;
        }

        await Services.OpenUriAsync(link.Url).ConfigureAwait(true);
    }


    [RelayCommand]
    private void OpenLogFolder()
    {
        Status = SettingShellActions.TryOpen(LogDirectory) is { } error
            ? string.Format(Strings["Settings.Common.OpenFailed"], error)
            : null;
    }


    public override void Dispose()
    {
        Links.Clear();
        base.Dispose();
    }


    private static string ReadVersion()
    {
        var assembly = typeof(SettingAboutViewModel).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // 去掉 SourceLink 的后缀（1.0.0+abcdef）
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}


/// <summary>关于页的一条外链（Starward 用 Hyperlink + Run）。</summary>
public sealed record SettingLinkViewModel(string Label, string Url);
