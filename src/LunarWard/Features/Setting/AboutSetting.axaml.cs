using LunarWard.Features.Update;
using LunarWard.Language;
using LunarWard.Services;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Frameworks;
using LunarWard.Platform;

namespace LunarWard.Features.Setting;

/// <summary>关于页（1:1 复刻 Starward <c>AboutSetting.xaml</c>）。</summary>
/// <remarks>
/// <para>
/// 控件顺序 / 间距照抄 Starward：Logo(160) → 24px 应用名 → 副标题 → 版本 →
/// 预览版渠道 → 检查更新 → 「相关链接」+ 5 条粗体链接。
/// </para>
/// <para>
/// 「检查更新 / 预览版通道」与上游一致：<see cref="UpdateService"/> 取 GitHub Release，
/// 有新版弹 <see cref="UpdateWindow"/>。
/// 相关链接里的「隐私策略 / 赞助开发者」在 LunarWard 没有对应页面，同样保留位置并禁用。
/// </para>
/// <para>
/// LunarWard 追加的「上游 Starward / 文档」两条链接排在 Starward 五条之后；
/// 复刻声明 / 许可 / 致谢排在全部 Starward 条目之后。
/// </para>
/// </remarks>
public sealed partial class AboutSetting : PageBase
{

    public new event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([global::System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [global::System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (global::System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
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

    public AboutSetting()
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();
        Version = AppVersion.Display;

        // LunarWard 追加：Starward 的关于页只有应用版本，没有系统环境；
        // Linux 上显卡/会话信息对排查启动问题非常有用，放在版本号下面。
        System = LinuxSystemInfo.Read();

        // 顺序 = AboutSetting.xaml:92-126（项目主页 / 反馈问题 / 更新记录 / 隐私策略 / 赞助开发者）
        // + LunarWard 追加的两条（上游 Starward / 文档）。
        Links =
        [
            new SettingLinkViewModel(Lang.SettingPage_ProjectHomepage, "https://github.com/QiE2035/LunarWard"),
            new SettingLinkViewModel(Lang.SettingPage_ReportIssue, "https://github.com/QiE2035/LunarWard/issues"),
            new SettingLinkViewModel(Lang.SettingPage_ReleaseHistroy, "https://github.com/QiE2035/LunarWard/releases"),
            // LunarWard 不收集个人数据，也没有单独的隐私策略 / 赞助页面：
            // 保留 Starward 的链接位但禁用，而不是指向一个不相干的页面。
            new SettingLinkViewModel(Lang.SettingPage_PrivacyPolicy, null,
                "LunarWard 不收集个人数据，也没有单独的隐私策略文档。"),
            new SettingLinkViewModel(Lang.SettingPage_Donate, null,
                "LunarWard 没有赞助页面。"),
            new SettingLinkViewModel("上游 Starward", "https://github.com/Scighost/Starward"),
            new SettingLinkViewModel("文档", "https://github.com/QiE2035/LunarWard/blob/main/docs/07-UI-REPLICA.md"),
        ];
        DataContext = this;
    }


    /// <summary>应用名（Starward 里是 24px Bold 的「Starward」，AboutSetting.xaml:24-27）。</summary>
    public string AppName => "LunarWard";

    public string Version { get; }


    /// <summary>Linux 运行环境（内核 / 会话 / 显卡），只读 <c>/proc</c>、<c>/sys</c>。</summary>
    public LinuxSystemInfo System { get; }

    public List<SettingLinkViewModel> Links { get; }


    private readonly ILogger<AboutSetting> _logger = AppConfig.GetLogger<AboutSetting>();


    /// <summary>预览版更新渠道（AboutSetting.xaml.cs:29-42 同名属性）。</summary>
    private bool _enablePreviewRelease = AppConfig.EnablePreviewRelease;

    public bool EnablePreviewRelease
    {
        get => _enablePreviewRelease;
        set
        {
            if (SetProperty(ref _enablePreviewRelease, value))
            {
                AppConfig.EnablePreviewRelease = value;
            }
        }
    }


    /// <summary>已是最新版本（AboutSetting.xaml.cs:47-51）。</summary>
    private string? _latestVersion;

    public string? LatestVersion
    {
        get => _latestVersion;
        set => SetProperty(ref _latestVersion, value);
    }


    /// <summary>检查更新的错误文本（AboutSetting.xaml.cs:54-58）。</summary>
    private string? _updateErrorText;

    public string? UpdateErrorText
    {
        get => _updateErrorText;
        set => SetProperty(ref _updateErrorText, value);
    }


    /// <summary>
    /// 检查更新（AboutSetting.xaml.cs:62-84 原样对齐上游）：
    /// <c>UpdateService.GetLatestVersionAsync</c> → NuGetVersion 比较 → 有新版弹 <see cref="UpdateWindow"/>，
    /// 否则把 <c>release.Version</c> 写进 <see cref="LatestVersion"/>；异常写 <see cref="UpdateErrorText"/> + log。
    /// </summary>
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        try
        {
            LatestVersion = null;
            UpdateErrorText = null;
            var release = await AppConfig.GetService<UpdateService>().GetLatestVersionAsync();
            _ = NuGetVersion.TryParse(AppConfig.AppVersion, out var currentVersion);
            _ = NuGetVersion.TryParse(release.Version, out var newVersion);
            if (newVersion! > currentVersion!)
            {
                new UpdateWindow { NewVersion = release }.Activate();
            }
            else
            {
                LatestVersion = release.Version;
            }
        }
        catch (Exception ex)
        {
            UpdateErrorText = ex.Message;
            _logger.LogError(ex, "Check update");
        }
    }


    [RelayCommand]
    private void OpenLink(SettingLinkViewModel? link)
    {
        if (link?.Url is null)
        {
            return;
        }

        ShellHelper.OpenUri(link.Url);
    }


    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        Links.Clear();
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
