using System.Reflection;
using LunarWard.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>关于页（1:1 复刻 Starward <c>AboutSetting.xaml</c>）。</summary>
/// <remarks>
/// <para>
/// 控件顺序 / 间距照抄 Starward：Logo(160) → 24px 应用名 → 副标题 → 版本 →
/// 预览版渠道 → 检查更新 → 「相关链接」+ 5 条粗体链接。
/// </para>
/// <para>
/// Starward 有「检查更新 / 预览版通道」；LunarWard 走发行包升级，不内置更新器，
/// 因此这两个控件保留位置但禁用（<c>AboutSetting.xaml:43-85</c>），旁边写清原因。
/// 相关链接里的「隐私策略 / 赞助开发者」在 LunarWard 没有对应页面，同样保留位置并禁用。
/// </para>
/// <para>
/// LunarWard 追加的「上游 Starward / 文档」两条链接排在 Starward 五条之后；
/// 复刻声明 / 许可 / 致谢排在全部 Starward 条目之后。
/// </para>
/// </remarks>
public sealed partial class SettingAboutViewModel : SettingSectionViewModel
{
    public SettingAboutViewModel(PageContext context, bool showDivider = false)
        : base(context, SettingSectionKind.About, "Settings.About", "\uE946", showDivider)
    {
        Version = ReadVersion();

        // 顺序 = AboutSetting.xaml:92-126（项目主页 / 反馈问题 / 更新记录 / 隐私策略 / 赞助开发者）
        // + LunarWard 追加的两条（上游 Starward / 文档）。
        Links =
        [
            new SettingLinkViewModel(Strings["Settings.About.Homepage"], "https://github.com/QiE2035/LunarWard"),
            new SettingLinkViewModel(Strings["Settings.About.Issues"], "https://github.com/QiE2035/LunarWard/issues"),
            new SettingLinkViewModel(Strings["Settings.About.Releases"], "https://github.com/QiE2035/LunarWard/releases"),
            // LunarWard 不收集个人数据，也没有单独的隐私策略 / 赞助页面：
            // 保留 Starward 的链接位但禁用，而不是指向一个不相干的页面。
            new SettingLinkViewModel(Strings["Settings.About.Privacy"], null,
                Strings["Settings.About.PrivacyUnavailable"]),
            new SettingLinkViewModel(Strings["Settings.About.Donate"], null,
                Strings["Settings.About.DonateUnavailable"]),
            new SettingLinkViewModel(Strings["Settings.About.Upstream"], "https://github.com/Scighost/Starward"),
            new SettingLinkViewModel(Strings["Settings.About.Docs"], "https://github.com/QiE2035/LunarWard/blob/main/docs/07-UI-REPLICA.md"),
        ];
    }


    /// <summary>应用名（Starward 里是 24px Bold 的「Starward」，AboutSetting.xaml:24-27）。</summary>
    public string AppName => "LunarWard";

    public string Version { get; }

    public List<SettingLinkViewModel> Links { get; }


    /// <summary>检查更新进行中（对应上游 <c>ProgressRing</c> 的位置）。</summary>
    [ObservableProperty]
    private bool _isCheckingUpdate;


    /// <summary>检查结果文案（已是最新 / 有新版本 / 失败原因）。</summary>
    [ObservableProperty]
    private string? _updateMessage;


    /// <summary>有新版时为 <c>true</c>，显示「打开下载页」。</summary>
    [ObservableProperty]
    private bool _hasUpdate;


    private string? _updateUrl;


    /// <summary>
    /// 检查更新。上游会下载安装包并替换自身；LunarWard 走发行包，只在有新版时
    /// 给出发布页链接（见 <see cref="ReleaseChecker"/>）。
    /// </summary>
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        if (IsCheckingUpdate)
        {
            return;
        }

        IsCheckingUpdate = true;
        HasUpdate = false;
        UpdateMessage = Strings["Settings.About.Checking"];
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var current = typeof(SettingAboutViewModel).Assembly.GetName().Version ?? new Version(1, 0, 0);
            var result = await ReleaseChecker.CheckAsync(http, current).ConfigureAwait(true);
            switch (result.Status)
            {
                case ReleaseCheckStatus.UpdateAvailable:
                    HasUpdate = true;
                    _updateUrl = result.HtmlUrl;
                    UpdateMessage = string.Format(Strings["Settings.About.UpdateAvailable"], result.LatestVersion ?? "");
                    break;
                case ReleaseCheckStatus.UpToDate:
                    UpdateMessage = string.Format(Strings["Settings.About.UpToDate"], result.LatestVersion ?? Version);
                    break;
                default:
                    UpdateMessage = string.Format(Strings["Settings.About.CheckFailed"], result.Error ?? "");
                    break;
            }
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }


    [RelayCommand]
    private async Task OpenUpdateAsync()
    {
        if (_updateUrl is { Length: > 0 } url)
        {
            await Services.OpenUriAsync(url).ConfigureAwait(true);
        }
    }


    [RelayCommand]
    private async Task OpenLinkAsync(SettingLinkViewModel? link)
    {
        if (link?.Url is null)
        {
            return;
        }

        await Services.OpenUriAsync(link.Url).ConfigureAwait(true);
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


/// <summary>
/// 关于页的一条外链（Starward 用 <c>Hyperlink</c> + <c>Run</c>，这里用透明按钮等价物）。
/// </summary>
/// <param name="Label">链接文字。</param>
/// <param name="Url">目标地址；<c>null</c> 表示 LunarWard 没有对应页面，条目禁用。</param>
/// <param name="Note">禁用原因（Tooltip）。</param>
public sealed record SettingLinkViewModel(string Label, string? Url, string? Note = null)
{
    public bool IsEnabled => Url is not null;
}
