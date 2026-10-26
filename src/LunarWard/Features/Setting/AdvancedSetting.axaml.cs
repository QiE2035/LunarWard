using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Views.Starward;
using LunarWard.Launch;
using UrlProtocol = LunarWard.Launch.UrlProtocol;

namespace LunarWard.Features.Setting;

/// <summary>高级页里的一行环境变量默认值。</summary>
public sealed record SettingEnvRowViewModel(string Key, string Value, string Source);


/// <summary>
/// 高级页（对应 Starward <c>AdvancedSetting.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 这里是 URL 协议 + RPC 服务。LunarWard 没有 RPC 服务，但多了
/// 「十层 env 的基线默认值」与自检截图命令——这是本项目独有内容最多的一页。
/// </para>
/// <para>
/// 排版仍按 Starward 的节奏：标题 20px → 说明 14px 次要色 → 控件/按钮。
/// URL 协议状态如实探测 <c>.desktop</c>，不做"假装已注册"的开关。
/// </para>
/// </remarks>
public sealed partial class AdvancedSetting : SettingPageBase
{
    public AdvancedSetting(PageContext context)
        : base(context)
    {
        InitializeComponent();
        UrlProtocolStatus = ProbeUrlProtocol();
        EnvRows = new ObservableCollection<SettingEnvRowViewModel>();
        LoadEnvDefaults();
        DataContext = this;
    }


    // ── URL 协议 ──────────────────────────────────────────────────────

    private bool _isUrlRegistered;

    public bool IsUrlRegistered
    {
        get => _isUrlRegistered;
        set => SetProperty(ref _isUrlRegistered, value);
    }


    private string _urlProtocolStatus = "";

    public string UrlProtocolStatus
    {
        get => _urlProtocolStatus;
        set => SetProperty(ref _urlProtocolStatus, value);
    }


    /// <summary>协议说明（<c>lunarward://</c> 的 MimeType 来自 <see cref="UrlProtocol"/>）。</summary>
    public string MimeType => UrlProtocol.MimeType;


    private string? _testResult;

    public string? TestResult
    {
        get => _testResult;
        set => SetProperty(ref _testResult, value);
    }


    [RelayCommand]
    private void TestUrlProtocol()
    {
        var command = UrlProtocol.TryParse([$"{UrlProtocol.Scheme}://launch?profile=genshin-cn"], out var error);
        TestResult = command is null
            ? error ?? "未解析"
            : string.Format(CultureInfo.CurrentCulture, Strings["Settings.Advanced.TestResult"], UrlProtocol.Describe(command));
    }


    private string ProbeUrlProtocol()
    {
        try
        {
            var entry = SettingShellActions.FindDesktopEntry();
            if (entry is null)
            {
                IsUrlRegistered = false;
                return Strings["Settings.Advanced.UrlNotRegistered"];
            }

            var content = File.ReadAllText(entry);
            IsUrlRegistered = UrlProtocol.IsDesktopEntryValid(content);
            return IsUrlRegistered
                ? $"{Strings["Settings.Advanced.UrlRegistered"]} · {entry}"
                : $"{Strings["Settings.Advanced.UrlNotRegistered"]} · {entry}";
        }
        catch (Exception ex)
        {
            IsUrlRegistered = false;
            return ex.Message;
        }
    }


    // ── 日志 ──────────────────────────────────────────────────────────

    public string LogLevelValue => Strings["Settings.Advanced.LogLevelValue"];

    public string LogDirectory => Services.Paths.LogDirectory;


    [RelayCommand]
    private void OpenLogFolder()
    {
        Status = SettingShellActions.TryOpen(LogDirectory) is { } error
            ? string.Format(CultureInfo.CurrentCulture, Strings["Settings.Common.OpenFailed"], error)
            : null;
    }


    // ── 环境变量默认值（十层合并器的基线层）────────────────────────────

    public ObservableCollection<SettingEnvRowViewModel> EnvRows { get; }


    private bool _hasEnv;

    public bool HasEnv
    {
        get => _hasEnv;
        set => SetProperty(ref _hasEnv, value);
    }


    private string? _envStatus;

    public string? EnvStatus
    {
        get => _envStatus;
        set => SetProperty(ref _envStatus, value);
    }


    /// <summary>
    /// 用当前档案 dry-run 一遍启动计划，只取基线层的键（<c>WINEPREFIX</c>、
    /// <c>WINEDLLOVERRIDES</c> 等）。构建失败时显示说明而不是空白表。
    /// </summary>
    private void LoadEnvDefaults()
    {
        try
        {
            var plan = Services.LauncherService.BuildPlan(Profile.Id, Profile.Profile, dryRun: true);
            EnvRows.Clear();
            foreach (var (key, value) in plan.Env.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var source = plan.EnvSources.TryGetValue(key, out var layer) ? layer : "";
                EnvRows.Add(new SettingEnvRowViewModel(key, value, source));
            }

            HasEnv = EnvRows.Count > 0;
            EnvStatus = HasEnv ? null : Strings["Settings.Advanced.EnvUnavailable"];
        }
        catch (Exception ex)
        {
            HasEnv = false;
            EnvStatus = $"{Strings["Settings.Advanced.EnvUnavailable"]}（{ex.Message}）";
        }
    }


    // ── 自检 ──────────────────────────────────────────────────────────

    /// <summary>自检命令（与 <c>Program.cs</c> 的 <c>--screenshot</c> 一致）。</summary>
    public string SelfCheckCommand => "dotnet run --project src/LunarWard -- --screenshot /tmp/lunarward.png --page settings --wait 2500";


    [RelayCommand]
    private async Task CopySelfCheckAsync()
    {
        Status = await SettingShellActions.TryCopyAsync(SelfCheckCommand).ConfigureAwait(true) is { } error
            ? error
            : Strings["Settings.Common.Copied"];
    }
}
