using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Localization;
using LunarWard.App.Services;
using LunarWard.Launch;
using LunarWard.Wine;

namespace LunarWard.App.ViewModels;

/// <summary>
/// 主窗口 ViewModel。视图层只做绑定，所有逻辑在这里。
/// </summary>
/// <remarks>
/// 启动按钮状态机见 <c>docs/04-FEATURES.md</c> §5.2；
/// 外部程序面板见 §1.8。
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppServices _services;

    private readonly LogService _log;


    public MainWindowViewModel(AppServices services, LogService log)
    {
        _services = services;
        _log = log;

        _services.LauncherService.StateChanged += (_, state) => Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyState(state));
        _services.LauncherService.Completed += (_, result) => Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowResult(result));

        _background = services.Background;

        Profiles = new ObservableCollection<ProfileItemViewModel>();
        ReloadProfiles();

        ApplyState(LaunchPhaseState.Idle);

        Localization.AppStrings.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Item[]")
            {
                // 语言切换后，代码里缓存过文案的属性要重算
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ApplyState(_services.LauncherService.State);
                    if (SelectedProfile is { } p)
                    {
                        UpdatePlayTime(p.Id);
                    }
                });
            }
        };

        BackgroundAvailable = services.Background.IsVlcAvailable;
        BackgroundError = services.Background.ProbeError;
    }


    private readonly BackgroundService _background;


    /// <summary>外部可注入：主题切换。</summary>
    public ObservableCollection<ProfileItemViewModel> Profiles { get; }

    public ObservableCollection<AnnouncementViewModel> Announcements { get; } = [];

    public ObservableCollection<EnvironmentRowViewModel> EnvironmentRows { get; } = [];

    public ObservableCollection<ProgramItemViewModel> ExternalPrograms { get; } = [];

    public ObservableCollection<string> RecentLogLines { get; } = [];


    [ObservableProperty]
    private ProfileItemViewModel? selectedProfile;


    [ObservableProperty]
    private string launchButtonText = "";


    [ObservableProperty]
    private bool launchButtonEnabled = true;


    [ObservableProperty]
    private string statusText = "";


    [ObservableProperty]
    private string? errorText;


    [ObservableProperty]
    private bool isBusy;


    [ObservableProperty]
    private string selectedTheme = "Modern";


    [ObservableProperty]
    private bool backgroundAvailable;


    [ObservableProperty]
    private string? backgroundError;


    [ObservableProperty]
    private string runtimeExplain = "";


    [ObservableProperty]
    private bool showEnvironmentPanel;


    [ObservableProperty]
    private string? announcementHtml;


    /// <summary>没有拉到任何公告时显示占位（离线 / 接口不可用）。</summary>
    public bool HasNoAnnouncements => Announcements.Count == 0;


    [ObservableProperty]
    private bool hasGameRunning;


    /// <summary>当前档案的累计时长文案（如 "12 小时 34 分"）。</summary>
    [ObservableProperty]
    private string playTimeText = "";


    public IReadOnlyList<string> Themes { get; } = ["Modern", "Classic"];


    partial void OnSelectedProfileChanged(ProfileItemViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        _services.Config.Launcher.LastProfileIndex = Profiles.IndexOf(value);
        try
        {
            _services.Config.Save(_services.Paths.ConfigFile);
        }
        catch (Exception ex)
        {
            _log.Error("保存配置失败", ex);
        }

        RefreshProfileDetails(value);
    }


    partial void OnSelectedThemeChanged(string value)
    {
        var app = Avalonia.Application.Current;
        if (app is not null)
        {
            app.RequestedThemeVariant = value == "Modern"
                ? Avalonia.Styling.ThemeVariant.Dark
                : Avalonia.Styling.ThemeVariant.Default;
        }

        _services.Config.Launcher.Style = value;
    }


    /// <summary>重新读取 profiles。</summary>
    public void ReloadProfiles()
    {
        var previous = SelectedProfile?.Id;
        Profiles.Clear();
        foreach (var (id, profile) in _services.Config.Profiles)
        {
            var definition = Games.GameCatalog.Detect(ProfileConfig.ExpandPath(profile.Path));
            var display = definition?.DisplayName ?? profile.Game;
            Profiles.Add(new ProfileItemViewModel(id, profile, display));
        }

        if (Profiles.Count == 0)
        {
            // 首次运行：给一个可用的默认档（不自动扫描到任何路径，由用户设置）
            var profile = new ProfileConfig
            {
                Game = "genshin",
                Edition = "China",
                Path = "",
                Channel = "Official",
            };
            _services.Config.Profiles["genshin-cn"] = profile;
            Profiles.Add(new ProfileItemViewModel("genshin-cn", profile, "原神"));
        }

        EnsureDefaultRuntime();

        var index = Math.Clamp(_services.Config.Launcher.LastProfileIndex, 0, Profiles.Count - 1);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == previous) ?? Profiles[index];
    }


    /// <summary>
    /// 首次运行时挑一个可用运行时（系统 wine → 本项目托管 → 只读借用其它启动器 → Steam Proton），
    /// 避免开箱即"PATH 里找不到 wine"。用户可在设置里改。
    /// </summary>
    private void EnsureDefaultRuntime()
    {
        if (!string.IsNullOrEmpty(_services.Config.Launcher.DefaultRuntime)
            && _services.Config.Runtimes.ContainsKey(_services.Config.Launcher.DefaultRuntime))
        {
            return;
        }

        var available = new RuntimeScanner(_services.Paths).Scan();
        var picked = available.FirstOrDefault(x => x.Source == "System")
                     ?? available.FirstOrDefault(x => x.Source == "Managed")
                     ?? available.FirstOrDefault(x => x.Source == "Borrowed")
                     ?? available.FirstOrDefault(x => x.Source == "Proton");
        if (picked is null)
        {
            return;
        }

        _services.Config.Runtimes[picked.Name] = new RuntimeConfigDto
        {
            Runner = RunnerDto.FromDomain(picked.Runner),
            Dxvk = new DxvkDto { Kind = "System" },
        };
        _services.Config.Launcher.DefaultRuntime = picked.Name;

        foreach (var profile in _services.Config.Profiles.Values)
        {
            profile.Runtime = new ScopedRuntimeDto { Kind = "Default" };
        }

        try
        {
            _services.Config.Save(_services.Paths.ConfigFile);
            _log.Info($"首次运行自动选择运行时：{picked.Name}（{picked.Path}）");
        }
        catch (Exception ex)
        {
            _log.Error("保存默认运行时失败", ex);
        }
    }


    /// <summary>供视图在窗口打开后调用（触发一次 env 表格填充）。</summary>
    public void RefreshProfileDetailsIfSelected()
    {
        if (SelectedProfile is { } item)
        {
            RefreshProfileDetails(item);
        }
    }


    /// <summary>背景服务（视图用来挂载视频/图片）。</summary>
    public BackgroundService BackgroundService => _background;


    private void RefreshProfileDetails(ProfileItemViewModel item)
    {
        UpdatePlayTime(item.Id);
        RefreshExternalPrograms(item);
        RefreshEnvironment(item);
    }


    private void RefreshExternalPrograms(ProfileItemViewModel item)
    {
        ExternalPrograms.Clear();
        var keywords = BuildKeywords(item.Profile);
        foreach (var program in item.Profile.ExternalPrograms)
        {
            ExternalPrograms.Add(new ProgramItemViewModel(program, keywords));
        }
    }


    private IReadOnlyDictionary<string, string?> BuildKeywords(ProfileConfig profile)
    {
        var gameDirectory = ProfileConfig.ExpandPath(profile.Path);
        return new KeywordReplacer.Builder()
            .Game(gameDirectory)
            .Exe(Games.GameCatalog.Detect(gameDirectory)?.Executable ?? "YuanShen.exe")
            .Launcher(_services.Paths.DataHome)
            .Prefix(PrefixResolver.Resolve(profile.Prefix.ToDomain(), SelectedProfile?.Id ?? "default", _services.Paths))
            .Temp(Path.GetTempPath())
            .Build();
    }


    /// <summary>构建计划并把 env 表格刷出来（不启动）。</summary>
    public void RefreshEnvironment(ProfileItemViewModel item)
    {
        EnvironmentRows.Clear();
        try
        {
            var plan = _services.LauncherService.BuildPlan(item.Id, item.Profile, dryRun: true);
            RuntimeExplain = plan.Runtime.Explain;
            foreach (var (key, value) in plan.Env.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                EnvironmentRows.Add(new EnvironmentRowViewModel(key, value, plan.EnvSources.GetValueOrDefault(key, "?")));
            }
        }
        catch (RuntimeNotFoundException ex)
        {
            RuntimeExplain = $"运行时不可用：{ex.Detail}";
        }
        catch (Exception ex)
        {
            RuntimeExplain = ex.Message;
        }
    }


    [RelayCommand]
    private async Task LaunchAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (_services.LauncherService.State == LaunchPhaseState.Running)
        {
            _services.LauncherService.RequestStop();
            StatusText = "正在停止…";
            return;
        }

        ErrorText = null;
        IsBusy = true;

        try
        {
            var result = await _services.LauncherService.StartAsync(SelectedProfile.Id, SelectedProfile.Profile);
            if (!result.Success && result.Error is { } error)
            {
                ErrorText = error.Message;
                _log.Warn($"启动失败（{error.ExitCode}）：{error.Message}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }


    [RelayCommand]
    private void OpenEnvironmentShell()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var plan = _services.LauncherService.BuildPlan(SelectedProfile.Id, SelectedProfile.Profile, dryRun: true);
            var shell = new Views.ShellWindow
            {
                DataContext = new ShellViewModel(plan, _services.Paths),
            };
            shell.Show();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    [RelayCommand]
    private void OpenSettings()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var window = new Views.SettingsWindow
        {
            DataContext = new SettingsViewModel(_services, SelectedProfile),
        };
        window.Closed += (_, _) =>
        {
            ReloadProfiles();
            RefreshProfileDetailsIfSelected();
        };
        window.Show();
    }


    [RelayCommand]
    private void OpenGacha()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var window = new Views.GachaWindow
        {
            DataContext = new GachaViewModel(_services, SelectedProfile),
        };
        window.Show();
    }


    [RelayCommand]
    private void OpenDailyNote()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var window = new Views.DailyNoteWindow
        {
            DataContext = new DailyNoteViewModel(_services, SelectedProfile),
        };
        window.Show();
    }


    [RelayCommand]
    private void Diagnose()
    {
        try
        {
            var report = LaunchReport.ReadFrom(_services.Paths.LaunchReportFile);
            if (report is null)
            {
                ErrorText = "还没有启动记录（launch-report.json 不存在）";
                return;
            }

            var viewer = new Views.DiagnosticWindow
            {
                DataContext = new DiagnosticViewModel(report, _services.Paths),
            };
            viewer.Show();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    [RelayCommand]
    private void ToggleEnvironmentPanel() => ShowEnvironmentPanel = !ShowEnvironmentPanel;


    /// <summary>
    /// 执行一条 <c>lunarward://</c> 命令（见 <see cref="LunarWard.Launch.UrlProtocol"/>）。
    /// </summary>
    public void ApplyUrlCommand(LunarWard.Launch.UrlProtocolCommand command)
    {
        var profileId = command.Get("profile");
        if (profileId is not null)
        {
            var match = Profiles.FirstOrDefault(p => p.Id == profileId);
            if (match is not null)
            {
                SelectedProfile = match;
            }
            else
            {
                ErrorText = $"找不到档案：{profileId}";
                _log.Warn($"URL 协议引用了不存在的档案：{profileId}");
                return;
            }
        }

        switch (command.Verb)
        {
            case "launch":
                _ = LaunchAsync();
                break;
            case "shell":
                OpenEnvironmentShell();
                break;
            case "gacha":
                OpenGacha();
                break;
            default:
                break;
        }
    }


    /// <summary>异步拉取公告（离线时静默降级，不阻塞 UI）。</summary>
    public async Task LoadAnnouncementsAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var gameBiz = Games.GameCatalog
                .Get(SelectedProfile.Profile.Game, string.Equals(SelectedProfile.Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
                    ? Games.GameEdition.Global
                    : Games.GameEdition.China)?.GameBiz ?? "hk4e_cn";

            var items = await _services.Announcements.FetchAsync(gameBiz).ConfigureAwait(false);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Announcements.Clear();
                foreach (var item in items)
                {
                    Announcements.Add(new AnnouncementViewModel(item.Title, item.Body, item.Source.ToString(), item.IsRead));
                }

                OnPropertyChanged(nameof(HasNoAnnouncements));
            });
        }
        catch (Exception ex)
        {
            _log.Warn($"拉取公告失败：{ex.Message}");
        }
    }


    [RelayCommand]
    private void SaveProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            _services.Config.Profiles[SelectedProfile.Id] = SelectedProfile.Profile;
            _services.Config.Save(_services.Paths.ConfigFile);
            StatusText = "已保存";
            RefreshProfileDetails(SelectedProfile);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    [RelayCommand]
    private void AddExternalProgram()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var program = new ExternalProgram
        {
            Id = "prog-" + Guid.NewGuid().ToString("N")[..8],
            Name = "新程序",
            Path = "",
            Trigger = Trigger.Manual,
        };
        SelectedProfile.Profile.ExternalPrograms.Add(program);
        RefreshExternalPrograms(SelectedProfile);
    }


    private void ApplyState(LaunchPhaseState state)
    {
        var (text, enabled) = LaunchButtonState.For(state);
        LaunchButtonText = text;
        LaunchButtonEnabled = enabled;
        HasGameRunning = state == LaunchPhaseState.Running;

        var strings = AppStrings.Instance;
        StatusText = state switch
        {
            LaunchPhaseState.Idle => strings["Main.StatusIdle"],
            LaunchPhaseState.Prepare => strings["Main.Preparing"],
            LaunchPhaseState.Launch => strings["Main.Starting"],
            LaunchPhaseState.AwaitImage => strings["Main.WaitingGame"],
            LaunchPhaseState.AwaitReady => strings["Main.WaitingReady"],
            LaunchPhaseState.PostLaunch => strings["Main.StartingHelpers"],
            LaunchPhaseState.Running => strings["Main.StatusRunning"],
            LaunchPhaseState.Cleanup => strings["Main.Finishing"],
            LaunchPhaseState.Error => strings["Main.StatusError"],
            _ => StatusText,
        };

        if (state == LaunchPhaseState.Running)
        {
            _background.Pause();
        }
        else if (state == LaunchPhaseState.Idle)
        {
            _background.Resume();
        }

        foreach (var program in ExternalPrograms)
        {
            program.CanStartNow = state == LaunchPhaseState.Running;
        }
    }


    private void ShowResult(LaunchResult result)
    {
        if (SelectedProfile is { } profile)
        {
            UpdatePlayTime(profile.Id);
        }

        if (result.Success)
        {
            StatusText = result.Signal is { } signal
                ? $"游戏已退出（就绪信号：{signal}）"
                : "游戏已退出";
        }
        else
        {
            ErrorText = result.Error?.Message;
        }
    }


    /// <summary>刷新时长文案。</summary>
    public void UpdatePlayTime(string profileId)
    {
        var total = _services.PlayTime.TotalFor(profileId);
        var strings = AppStrings.Instance;
        PlayTimeText = total <= TimeSpan.Zero
            ? strings["Main.NeverLaunched"]
            : $"{strings["Main.Accumulated"]} {FormatDuration(total)}";

        static string FormatDuration(TimeSpan span) => span.TotalHours >= 1
            ? $"{(int)span.TotalHours} 小时 {span.Minutes} 分"
            : $"{span.Minutes} 分 {span.Seconds} 秒";
    }


    public void Shutdown() => _services.LauncherService.Shutdown();
}


/// <summary>一个 profile 的可绑定视图。</summary>
public sealed partial class ProfileItemViewModel : ObservableObject
{
    public ProfileItemViewModel(string id, ProfileConfig profile, string displayName)
    {
        Id = id;
        Profile = profile;
        DisplayName = displayName;
    }


    public string Id { get; }

    public ProfileConfig Profile { get; }

    public string DisplayName { get; }

    public string Display => $"{DisplayName} · {Id}";


    public string Path
    {
        get => Profile.Path;
        set
        {
            Profile.Path = value;
            OnPropertyChanged();
        }
    }


    public string Channel
    {
        get => Profile.Channel;
        set
        {
            Profile.Channel = value;
            OnPropertyChanged();
        }
    }


    public IReadOnlyList<string> Channels => Games.ChannelCatalog.SupportedFor(
        Games.GameCatalog.Get(Profile.Game, string.Equals(Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
            ? Games.GameEdition.Global
            : Games.GameEdition.China) ?? Games.GameCatalog.GenshinChina)
        .Select(x => x.ToString())
        .ToList();
}


public sealed record EnvironmentRowViewModel(string Key, string Value, string Source);


/// <summary>
/// 公告的展示模型。
/// </summary>
/// <remarks>
/// 官方公告与版本亮点的 <c>Body</c> 是 Markdown（见 <c>docs/04-FEATURES.md</c> §5.5）。
/// Avalonia 没有内建 HTML 控件，这里用 <c>Markdig</c> 转成纯文本显示——
/// 而不是把 Markdown 标记原样丢给用户看。
/// </remarks>
public sealed record AnnouncementViewModel(string Title, string Body, string? Source, bool IsRead)
{
    /// <summary>渲染成可读纯文本（Markdown → plain text）。</summary>
    public string PlainBody { get; } = ToPlainText(Body);


    private static string ToPlainText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return "";
        }

        try
        {
            // 只做"去标记"，不渲染 HTML；链接保留文字即可
            var plain = Markdig.Markdown.ToPlainText(markdown);
            return plain.Trim();
        }
        catch (Exception)
        {
            return markdown;
        }
    }
}


/// <summary>外部程序面板的一行。</summary>
public sealed partial class ProgramItemViewModel : ObservableObject
{
    private readonly ExternalProgram _program;

    private readonly IReadOnlyDictionary<string, string?> _keywords;


    public ProgramItemViewModel(ExternalProgram program, IReadOnlyDictionary<string, string?> keywords)
    {
        _program = program;
        _keywords = keywords;
    }


    public ExternalProgram Model => _program;

    public string Id => _program.Id;

    public string Name
    {
        get => _program.Name;
        set
        {
            _program.Name = value;
            OnPropertyChanged();
        }
    }


    public bool Enabled
    {
        get => _program.Enabled;
        set
        {
            _program.Enabled = value;
            OnPropertyChanged();
        }
    }


    public string Path
    {
        get => _program.Path;
        set
        {
            _program.Path = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Warnings));
            OnPropertyChanged(nameof(Suggestion));
        }
    }


    public string Arguments
    {
        get => string.Join(' ', _program.Args);
        set
        {
            _program.Args = CommandLineSplitter.Split(value);
            OnPropertyChanged();
        }
    }


    /// <summary><c>Auto</c>（伴随启动）/ <c>Manual</c>（手动）。</summary>
    public IReadOnlyList<Trigger> Triggers { get; } = [Trigger.Auto, Trigger.Manual];

    public Trigger Trigger
    {
        get => _program.Trigger;
        set
        {
            _program.Trigger = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Warnings));
        }
    }


    public IReadOnlyList<ExitPolicy> ExitPolicies { get; } = [ExitPolicy.Kill, ExitPolicy.Keep];

    public ExitPolicy OnGameExit
    {
        get => _program.OnGameExit;
        set
        {
            _program.OnGameExit = value;
            OnPropertyChanged();
        }
    }


    public bool Wait
    {
        get => _program.Wait;
        set
        {
            _program.Wait = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Warnings));
        }
    }


    public string Warnings => string.Join(" ", _program.GetWarnings(_keywords).Select(w => w.Message));


    public string? Suggestion => _program.GetSuggestion();


    [ObservableProperty]
    private bool canStartNow;


    /// <summary>三态 env 行。</summary>
    public ObservableCollection<EnvEntryViewModel> Env => new(_program.Env.Select(e => new EnvEntryViewModel(e, _program.Env)));


    [RelayCommand]
    private void AddEnv()
    {
        _program.Env.Add(new EnvEntry { Key = "NEW_VAR", Kind = EnvPatchKind.Inherit });
        OnPropertyChanged(nameof(Env));
    }


    [RelayCommand]
    private void RemoveEnv(EnvEntryViewModel entry)
    {
        _program.Env.Remove(entry.Model);
        OnPropertyChanged(nameof(Env));
    }


    [RelayCommand]
    private void StartNow()
    {
        // 由 MainWindowViewModel 注入的实际动作；这里只发事件
        StartRequested?.Invoke(this, Id);
    }


    public event EventHandler<string>? StartRequested;
}


/// <summary>三态 env 的一行（继承 / 覆盖 / 删除，加"设为空值"副选项）。</summary>
public sealed partial class EnvEntryViewModel : ObservableObject
{
    private readonly List<EnvEntry> _owner;


    public EnvEntryViewModel(EnvEntry model, List<EnvEntry> owner)
    {
        Model = model;
        _owner = owner;
    }


    public EnvEntry Model { get; }

    public string Key
    {
        get => Model.Key;
        set
        {
            Model.Key = value;
            OnPropertyChanged();
        }
    }


    public IReadOnlyList<EnvPatchKind> Kinds { get; } =
        [EnvPatchKind.Inherit, EnvPatchKind.Override, EnvPatchKind.Remove];

    public EnvPatchKind Kind
    {
        get => Model.Kind;
        set
        {
            Model.Kind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverride));
        }
    }


    public bool IsOverride => Model.Kind == EnvPatchKind.Override;


    public string Value
    {
        get => Model.Value ?? "";
        set
        {
            // 输入框清空 → 默认视为 Remove（docs/00-DECISIONS.md D7）
            if (string.IsNullOrEmpty(value) && !SetEmptyValue)
            {
                Model.Kind = EnvPatchKind.Remove;
                Model.Value = null;
            }
            else
            {
                Model.Kind = EnvPatchKind.Override;
                Model.Value = value;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(Kind));
        }
    }


    /// <summary>"设为空值"副选项：勾上则用 <c>Override("")</c> 而不是 <c>Remove</c>。</summary>
    [ObservableProperty]
    private bool setEmptyValue;


    partial void OnSetEmptyValueChanged(bool value)
    {
        if (value)
        {
            Model.Kind = EnvPatchKind.Override;
            Model.Value = "";
        }
        else if (Model.Value == "")
        {
            Model.Kind = EnvPatchKind.Remove;
            Model.Value = null;
        }

        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(Value));
    }
}


/// <summary>启动报告查看器。</summary>
public sealed class DiagnosticViewModel
{
    public DiagnosticViewModel(LaunchReport report, Platform.IXdgPaths paths)
    {
        Report = report;
        Json = report.ToJson();
        ReportPath = paths.LaunchReportFile;
        GameLogPath = paths.GameLogFile;
        TroubleshootingSteps =
        [
            "1. pgrep -af 'wineserver|winedevice'    —— 有输出说明 prefix 是「热」的",
            "2. 检查 Prepare 相位是否有 PrefixNotClean",
            "3. 检查 AwaitImage 是否命中（有没有找到游戏 exe）",
            "4. 检查 AwaitReady 的 signal 是什么：Grace 且很快 → 保护期可能更长，调大 grace",
            "5. 看 game.log 是否有 wine 报错",
            "6. 看游戏目录的 driverError.log 是否有 initDriver Failed",
        ];
    }


    public LaunchReport Report { get; }

    public string Json { get; }

    public string ReportPath { get; }

    public string GameLogPath { get; }

    public IReadOnlyList<string> TroubleshootingSteps { get; }

    public string Summary =>
        $"{(Report.Success ? "成功" : "失败")} · 退出码 {Report.ExitCode}"
        + (Report.ReadySignal is null ? "" : $" · 信号 {Report.ReadySignal}")
        + (Report.GamePid is null ? "" : $" · pid {Report.GamePid}");
}
