using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Frameworks;
using LunarWard.Launch;

namespace LunarWard.Features.Setting;

/// <summary>
/// 高级页（对应 Starward <c>AdvancedSetting.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 这里是 URL 协议 + RPC 服务。LunarWard 没有 RPC 服务，保留 URL 协议探测；
/// 上游没有环境变量/BuildPlan 区块，本地旧壳曾加过，现已删除（见 docs/09 第十节）。
/// </para>
/// <para>
/// 排版仍按 Starward 的节奏：标题 20px → 说明 14px 次要色 → 控件/按钮。
/// URL 协议状态如实探测 <c>.desktop</c>，不做"假装已注册"的开关。
/// </para>
/// </remarks>
public sealed partial class AdvancedSetting : PageBase
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

    private static Localization.AppStrings Strings => Localization.AppStrings.Instance;

    private static AppServices Services => AppServices.Current;

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
    public AdvancedSetting()
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();
        UrlProtocolStatus = ProbeUrlProtocol();
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
    public string MimeType => LunarWard.Launch.UrlProtocol.MimeType;


    private string? _testResult;

    public string? TestResult
    {
        get => _testResult;
        set => SetProperty(ref _testResult, value);
    }


    [RelayCommand]
    private void TestUrlProtocol()
    {
        var command = LunarWard.Launch.UrlProtocol.TryParse([$"{LunarWard.Launch.UrlProtocol.Scheme}://launch?profile=genshin-cn"], out var error);
        TestResult = command is null
            ? error ?? "未解析"
            : string.Format(CultureInfo.CurrentCulture, Strings["Settings.Advanced.TestResult"], LunarWard.Launch.UrlProtocol.Describe(command));
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
            IsUrlRegistered = LunarWard.Launch.UrlProtocol.IsDesktopEntryValid(content);
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
