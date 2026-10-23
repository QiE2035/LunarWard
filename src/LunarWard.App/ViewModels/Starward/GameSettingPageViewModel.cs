using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Localization;
using LunarWard.App.Services;
using LunarWard.App.Views.Starward;
using LunarWard.Games;
using LunarWard.Launch;
using LunarWard.Wine;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 游戏设置页（对应 Starward <c>GameSettingPage</c>）：左列基础设置 + 右列高级设置 + 底部 60px 应用条。
/// </summary>
/// <remarks>
/// <para>
/// 排版骨架来自 <c>Starward/Features/GameSetting/GameSettingPage.xaml</c>；Starward 原页里的
/// Windows 专属项（分辨率 / HDR / 语音语言 / 帧率）在 Linux 上没有对应值，
/// <b>不伪造</b>——左列换成 LunarWard 真正拥有的基础设置（目录 / 渠道 / 运行时 / wine 选项）。
/// </para>
/// <para>
/// 右列是 <c>docs/04-FEATURES.md</c> 要求的追加区：十层合并 env 表（§4.3）、
/// 外部程序（§1.8）、诊断（launch-report.json）。三者都只是把既有服务的结果<b>展示</b>出来，
/// 合并算法与启动逻辑仍在 <see cref="LauncherService"/> / <see cref="LaunchPlanFactory"/> 里，
/// 页面不复制一份。
/// </para>
/// </remarks>
public sealed partial class GameSettingPageViewModel : StarwardPageViewModel
{
    private readonly LauncherService _launcher;

    private LaunchPlan? _plan;

    private bool _disposed;

    private bool _loading;


    public GameSettingPageViewModel(PageContext context) : base(context)
    {
        _launcher = Services.LauncherService;

        var profile = Profile.Profile;

        SyncOptions = GameSettingSyncOption.All;
        HudOptions = GameSettingHudOption.CreateAll(profile.Enhancements.Hud);
        ChannelOptions = BuildChannelOptions(profile);
        Runtimes = [];

        _loading = true;
        GamePath = profile.Path;
        SelectedChannel = ChannelOptions.FirstOrDefault(x =>
                             string.Equals(x.Key, profile.Channel, StringComparison.OrdinalIgnoreCase))
                         ?? ChannelOptions[0];
        SelectedSync = SyncOptions.FirstOrDefault(x => x.Value == profile.Wine.Sync) ?? SyncOptions[0];
        SelectedHud = HudOptions.FirstOrDefault(x =>
                         string.Equals(x.Value, profile.Enhancements.Hud, StringComparison.OrdinalIgnoreCase))
                     ?? HudOptions[0];
        Borderless = profile.Wine.Borderless;
        WineWayland = profile.Wine.WineWayland;
        TimeoutFix = profile.Wine.TimeoutFix;
        Fsr = profile.Enhancements.Fsr;
        Dx12 = profile.Enhancements.Dx12;
        ReadyGraceMsValue = Math.Clamp(profile.ReadyGraceMs, 0, 600_000);
        StartArguments = string.Join(' ', profile.Args);
        _loading = false;

        ScanRuntimes();
        ScanGames();

        ReloadPrograms();
        ReloadDiagnostics();
        RefreshChannelCheck();
        RefreshDirectoryCheck();
        RefreshPlan();

        _launcher.StateChanged += OnLauncherStateChanged;
        ApplyLauncherState(_launcher.State);
        SetDirty(false);
    }


    // ── 页头 / 状态 ──────────────────────────────────────────────────

    public string GameName => Profile.DisplayName;

    /// <summary>左侧标题下的一行小字：游戏 + 档案 id。</summary>
    public string GameSubtitle => $"{Profile.DisplayName} · {Profile.Id}";

    /// <summary>DX12 只对绝区零有意义（原神的 DX12 不走 DXVK，见 GameAdapter）。</summary>
    public bool IsZzz => string.Equals(Profile.Profile.Game, GameCatalog.Zzz, StringComparison.OrdinalIgnoreCase);


    // ── 左列：游戏目录 ────────────────────────────────────────────────

    [ObservableProperty]
    private string _gamePath = "";


    partial void OnGamePathChanged(string value)
    {
        if (_loading)
        {
            // 构造时回填输入框，不应把页面标成"有改动"（应用按钮只在用户真的改了东西时可用）
            return;
        }

        Profile.Profile.Path = value;
        SetDirty(true);
        RefreshDetectedGameFlags();
        RefreshDirectoryCheck();
        RefreshChannelCheck();
        RefreshPlan();
    }


    /// <summary>输入框里的目录是否是一个可识别的游戏目录（Starward 的"游戏未安装"提示等价物）。</summary>
    private void RefreshDirectoryCheck()
    {
        var directory = ProfileConfig.ExpandPath(GamePath);
        if (string.IsNullOrEmpty(directory))
        {
            // 还没选目录是"未配置"，不是"配错了"：用三级文字，不要一进来就报警
            HasGameDirectory = false;
            DirectoryInvalid = false;
            IsGamePathEmpty = true;
            GameDirectoryName = Strings["GameSetting.DirectoryMissing"];
            OnPropertyChanged(nameof(ShowDirectoryPlaceholder));
            return;
        }

        var definition = GameCatalog.Detect(directory);
        HasGameDirectory = definition is not null;
        DirectoryInvalid = definition is null;
        IsGamePathEmpty = false;
        OnPropertyChanged(nameof(ShowDirectoryPlaceholder));
        GameDirectoryName = definition is not null
            ? $"{definition.DisplayName} · {definition.Executable}"
            : Strings["GameSetting.DirectoryUnrecognized"];
    }


    /// <summary>目录存在但不像游戏目录（缺 exe）时才用 Caution 色。</summary>
    [ObservableProperty]
    private bool _directoryInvalid;


    /// <summary>输入框为空：显示"还没选目录"的占位小字（不用警告色）。</summary>
    [ObservableProperty]
    private bool _isGamePathEmpty = true;


    /// <summary>占位只在"没路径可选"时显示；扫描出候选目录后交给列表，避免两行重复文案。</summary>
    public bool ShowDirectoryPlaceholder => IsGamePathEmpty && DetectedGames.Count == 0;


    [ObservableProperty]
    private bool _hasGameDirectory;


    [ObservableProperty]
    private string _gameDirectoryName = "";


    public ObservableCollection<DetectedGameRow> DetectedGames { get; } = [];


    [ObservableProperty]
    private string _directoryStatus = "";


    [RelayCommand]
    private void ScanGames()
    {
        DetectedGames.Clear();
        IReadOnlyList<DetectedGame> found;
        try
        {
            found = Services.Scanner.Scan();
        }
        catch (Exception ex)
        {
            DirectoryStatus = string.Format(Strings["GameSetting.ScanFailed"], ex.Message);
            return;
        }

        foreach (var game in found)
        {
            DetectedGames.Add(new DetectedGameRow(game, UseDetectedGameCommand));
        }

        RefreshDetectedGameFlags();
        OnPropertyChanged(nameof(ShowDirectoryPlaceholder));
        DirectoryStatus = found.Count == 0
            ? Strings["GameSetting.ScanEmpty"]
            : string.Format(Strings["GameSetting.ScanFound"], found.Count);
    }


    private void RefreshDetectedGameFlags()
    {
        if (DetectedGames.Count == 0)
        {
            return;
        }

        var current = ProfileConfig.ExpandPath(GamePath);
        foreach (var row in DetectedGames)
        {
            row.Refresh(current, Profile.Profile.Game, Profile.Profile.Edition);
        }
    }


    [RelayCommand]
    private void UseDetectedGame(DetectedGameRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (!row.IsForCurrentGame)
        {
            // 换了游戏/版本就该换档案——那是左上角游戏选择器的事，这里不偷偷改 profile
            DirectoryStatus = string.Format(Strings["GameSetting.ScanOtherGame"], row.DisplayName);
            return;
        }

        GamePath = row.Directory;
        DirectoryStatus = string.Format(Strings["GameSetting.DirectoryApplied"], row.Directory);
        RefreshPlan();
    }


    // ── 左列：区服渠道 ────────────────────────────────────────────────

    public IReadOnlyList<GameSettingChannelOption> ChannelOptions { get; }

    private static IReadOnlyList<GameSettingChannelOption> BuildChannelOptions(ProfileConfig profile)
    {
        var definition = GameCatalog.Get(profile.Game, EditionOf(profile)) ?? GameCatalog.GetChina(profile.Game);
        var channels = definition?.SupportedChannels ?? [GameChannel.Official];
        var list = channels.Select(GameSettingChannelOption.FromChannel).ToList();
        return list.Count > 0 ? list : [GameSettingChannelOption.FromChannel(GameChannel.Official)];
    }


    [ObservableProperty]
    private GameSettingChannelOption _selectedChannel = null!;


    partial void OnSelectedChannelChanged(GameSettingChannelOption value)
    {
        if (value is null || _loading)
        {
            return;
        }

        Profile.Profile.Channel = value.Key;
        RefreshChannelCheck();
        SetDirty(true);
        RefreshPlan();
    }


    private string _channelMessage = "";

    private bool _channelBlocked;

    private bool _channelNeedsPlugin;


    /// <summary>渠道前置条件校验的结果（B 站三条件，见 <c>docs/04-FEATURES.md</c> §2.5）。</summary>
    public string ChannelMessage
    {
        get => _channelMessage;
        private set => SetProperty(ref _channelMessage, value);
    }


    public bool ChannelBlocked
    {
        get => _channelBlocked;
        private set => SetProperty(ref _channelBlocked, value);
    }


    public bool ChannelNeedsPlugin
    {
        get => _channelNeedsPlugin;
        private set => SetProperty(ref _channelNeedsPlugin, value);
    }


    [ObservableProperty]
    private string _pluginStatus = "";


    private void RefreshChannelCheck()
    {
        var profile = Profile.Profile;
        var directory = ProfileConfig.ExpandPath(GamePath);
        var definition = GameCatalog.Detect(directory)
                         ?? GameCatalog.Get(profile.Game, EditionOf(profile))
                         ?? GameCatalog.GetChina(profile.Game);

        if (definition is null)
        {
            ChannelMessage = "";
            ChannelBlocked = false;
            ChannelNeedsPlugin = false;
            return;
        }

        var channel = string.Equals(SelectedChannel?.Key, GameSettingChannelOption.Bilibili, StringComparison.Ordinal)
            ? GameChannel.Bilibili
            : GameChannel.Official;

        var check = ChannelValidator.Validate(channel, definition, directory);
        ChannelBlocked = !check.Ok;
        ChannelNeedsPlugin = check.NeedsPluginDownload;
        ChannelMessage = !check.Ok
            ? check.Error ?? ""
            : check.Warning ?? "";

        if (check.Ok && channel == GameChannel.Bilibili && ChannelMessage.Length == 0)
        {
            ChannelMessage = Strings["GameSetting.ChannelBilibiliReady"];
        }
    }


    [RelayCommand]
    private async Task DownloadBilibiliPluginAsync()
    {
        var profile = Profile.Profile;
        var directory = ProfileConfig.ExpandPath(GamePath);
        var definition = GameCatalog.Detect(directory) ?? GameCatalog.Get(profile.Game, EditionOf(profile));
        if (definition is null || string.IsNullOrEmpty(directory))
        {
            PluginStatus = Strings["GameSetting.PluginNoDirectory"];
            return;
        }

        PluginStatus = Strings["GameSetting.PluginDownloading"];
        try
        {
            var progress = new Progress<double>(p =>
                PluginStatus = string.Format(Strings["GameSetting.PluginProgress"], p * 100));

            var downloader = new BilibiliPluginDownloader(Services.Paths);
            var path = await downloader.InstallAsync(directory, definition, progress).ConfigureAwait(true);
            PluginStatus = string.Format(Strings["GameSetting.PluginInstalled"], path);
            Log.Info($"已安装 B 站插件：{path}");
            RefreshChannelCheck();
        }
        catch (Exception ex)
        {
            PluginStatus = string.Format(Strings["GameSetting.PluginFailed"], ex.Message);
            Log.Warn($"B 站插件安装失败：{ex.Message}");
        }
    }


    // ── 左列：wine 运行时 ─────────────────────────────────────────────

    public ObservableCollection<AvailableRuntime> Runtimes { get; }


    [ObservableProperty]
    private AvailableRuntime? _selectedRuntime;


    /// <summary>当前生效运行时的解释串（本地化：作用域 › runner 类型 + 二进制路径）。</summary>
    [ObservableProperty]
    private string _runtimeExplain = "";


    /// <summary>
    /// 从 <c>&lt;runner&gt;/bin/wine</c> 反推 runner 目录名（自定义/独占运行时用）。
    /// </summary>
    private static string RuntimeFolderName(string wineBinary)
    {
        var directory = Path.GetDirectoryName(wineBinary);
        if (string.IsNullOrEmpty(directory))
        {
            return Path.GetFileName(wineBinary);
        }

        // wine 一般在 <runner>/bin/wine；bin 的上一级才是 runner 根
        var parent = Path.GetDirectoryName(directory);
        return string.Equals(Path.GetFileName(directory), "bin", StringComparison.Ordinal) && !string.IsNullOrEmpty(parent)
            ? Path.GetFileName(parent)
            : Path.GetFileName(directory);
    }


    /// <summary>"为什么是这个运行时"（一行小字）。</summary>
    [ObservableProperty]
    private string _runtimeExplainNote = "";


    /// <summary>领域层原始解释串（ToolTip 用，含解析链细节与二进制路径）。</summary>
    [ObservableProperty]
    private string _runtimeExplainRaw = "";


    partial void OnSelectedRuntimeChanged(AvailableRuntime? value)
    {
        if (_loading || value is null)
        {
            return;
        }

        // 与旧 SettingsViewModel 相同：把选中的运行时写进 runtimes 表，profile 以 Shared 引用，
        // 这样"生效运行时"在 UI 与启动报告里都可解释（docs/04-FEATURES.md §3.1）。
        try
        {
            var config = Services.Config.Runtimes.TryGetValue(value.Name, out var existing)
                ? existing
                : new RuntimeConfigDto();
            config.Runner = RunnerDto.FromDomain(value.Runner);
            Services.Config.Runtimes[value.Name] = config;
            Profile.Profile.Runtime = new ScopedRuntimeDto { Kind = "Shared", Name = value.Name };
            Services.Config.Launcher.DefaultRuntime = value.Name;
            Log.Info($"默认运行时：{value.Name}（{value.Path}）");
            SetDirty(true);
            RefreshPlan();
        }
        catch (Exception ex)
        {
            PlanError = ex.Message;
        }
    }


    [RelayCommand]
    private void ScanRuntimes()
    {
        var previous = SelectedRuntime?.Name ?? Profile.Profile.Runtime.Name;
        Runtimes.Clear();
        IReadOnlyList<AvailableRuntime> found;
        try
        {
            found = new RuntimeScanner(Services.Paths).Scan();
        }
        catch (Exception ex)
        {
            RuntimeExplain = string.Format(Strings["GameSetting.RuntimeScanFailed"], ex.Message);
            return;
        }

        foreach (var runtime in found)
        {
            Runtimes.Add(runtime);
        }

        _loading = true;
        // 只在"确实等于当前默认运行时"时回填；否则留空，避免 UI 假装已经选了某个运行时
        // （profile 的 Runtime 仍是 Default 引用时，选一个只是视觉效果，不落盘也不生效）。
        SelectedRuntime = Runtimes.FirstOrDefault(x => x.Name == previous)
                          ?? Runtimes.FirstOrDefault(x => x.Name == Services.Config.Launcher.DefaultRuntime);
        _loading = false;
    }


    // ── 左列：wine 与增强 ─────────────────────────────────────────────

    public IReadOnlyList<GameSettingSyncOption> SyncOptions { get; }

    public IReadOnlyList<GameSettingHudOption> HudOptions { get; }


    [ObservableProperty]
    private GameSettingSyncOption _selectedSync = null!;


    partial void OnSelectedSyncChanged(GameSettingSyncOption value)
    {
        if (value is null || _loading)
        {
            return;
        }

        Profile.Profile.Wine.Sync = value.Value;
        SetDirty(true);
        RefreshPlan();
    }


    [ObservableProperty]
    private GameSettingHudOption _selectedHud = null!;


    partial void OnSelectedHudChanged(GameSettingHudOption value)
    {
        if (value is null || _loading)
        {
            return;
        }

        Profile.Profile.Enhancements.Hud = value.Value;
        SetDirty(true);
        RefreshPlan();
    }


    [ObservableProperty]
    private bool _borderless;


    partial void OnBorderlessChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        Profile.Profile.Wine.Borderless = value;
        SetDirty(true);
        RefreshPlan();
    }


    [ObservableProperty]
    private bool _wineWayland;


    partial void OnWineWaylandChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        Profile.Profile.Wine.WineWayland = value;
        SetDirty(true);
        RefreshPlan();
    }


    [ObservableProperty]
    private bool _timeoutFix;


    partial void OnTimeoutFixChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        Profile.Profile.Wine.TimeoutFix = value;
        SetDirty(true);
        RefreshPlan();
    }


    [ObservableProperty]
    private bool _fsr;


    partial void OnFsrChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        Profile.Profile.Enhancements.Fsr = value;
        SetDirty(true);
        RefreshPlan();
    }


    [ObservableProperty]
    private bool _dx12;


    partial void OnDx12Changed(bool value)
    {
        if (_loading)
        {
            return;
        }

        Profile.Profile.Enhancements.Dx12 = value;
        SetDirty(true);
        RefreshPlan();
    }


    /// <summary>游戏启动参数（追加在默认参数之后，支持 <c>%keywords%</c>）。</summary>
    [ObservableProperty]
    private string _startArguments = "";


    partial void OnStartArgumentsChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        Profile.Profile.Args = CommandLineSplitter.Split(value);
        SetDirty(true);
        RefreshPlan();
    }


    /// <summary><see cref="NumericUpDown.Value"/> 是 <c>decimal?</c>，这里做一次边界收敛。</summary>
    [ObservableProperty]
    private decimal? _readyGraceMsValue = 6000;


    partial void OnReadyGraceMsValueChanged(decimal? value)
    {
        if (_loading || value is null)
        {
            return;
        }

        var clamped = (int)Math.Clamp(value.Value, 0, 600_000);
        Profile.Profile.ReadyGraceMs = clamped;
        SetDirty(true);
        RefreshPlan();
    }


    // ── 右列：环境变量（十层合并）────────────────────────────────────

    public ObservableCollection<GameSettingEnvRow> EnvRows { get; } = [];


    [ObservableProperty]
    private string _planError = "";


    [ObservableProperty]
    private bool _hasPlanError;


    [ObservableProperty]
    private string _envSummary = "";


    [ObservableProperty]
    private string _prefixText = "";


    /// <summary>
    /// 一行"生效：&lt;作用域&gt; › &lt;运行时&gt;"，加一行"为什么"。
    /// </summary>
    /// <remarks>
    /// 不直接显示 <see cref="ResolvedRuntime.Explain"/>：那是领域层的中文固定串
    /// （<c>profile → 默认运行时 › 自定义 /path</c>），en-US 下会半句中文。
    /// 完整解析链与二进制路径放 <see cref="RuntimeExplainRaw"/> 做 ToolTip，排障不丢信息。
    /// 格式照 <c>docs/04-FEATURES.md</c> §3.1 的"结果必须可解释"。
    /// </remarks>
    private (string Headline, string Note) BuildRuntimeExplain(LaunchPlan plan)
    {
        var (scope, note) = Profile.Profile.Runtime.ToDomain() switch
        {
            Scoped<RuntimeConfig>.Shared shared => (
                string.Format(Strings["GameSetting.ScopeShared"], shared.Name),
                Strings["GameSetting.ScopeSharedNote"]),
            Scoped<RuntimeConfig>.PerGame => (
                Strings["GameSetting.ScopePerGame"],
                Strings["GameSetting.ScopePerGameNote"]),
            Scoped<RuntimeConfig>.Custom => (
                Strings["GameSetting.ScopeCustom"],
                Strings["GameSetting.ScopeCustomNote"]),
            _ => (
                Strings["GameSetting.ScopeDefault"],
                Strings["GameSetting.ScopeDefaultNote"]),
        };

        // 第二部分用"看得懂的运行时名"：具名运行时用自己的名字，否则用默认运行时名，
        // 再退到 wine 二进制所在目录名——不直接显示 Wine 层的 default/shared 内部标记。
        var runtimeName = Profile.Profile.Runtime.ToDomain() switch
        {
            Scoped<RuntimeConfig>.Shared shared => shared.Name,
            Scoped<RuntimeConfig>.Default => Services.Config.Launcher.DefaultRuntime,
            Scoped<RuntimeConfig>.PerGame or Scoped<RuntimeConfig>.Custom =>
                RuntimeFolderName(plan.WineBinary),
            _ => plan.Runtime.Name,
        };

        if (string.IsNullOrWhiteSpace(runtimeName))
        {
            runtimeName = Path.GetFileName(plan.Runtime.WineBinary) is { Length: > 0 } binary ? binary : plan.Runtime.Name;
        }

        var headline = string.Format(
            Strings["GameSetting.RuntimeResolved"],
            $"{scope} › {runtimeName}");

        return (headline, note);
    }


    /// <summary>
    /// 构建计划（不启动）并刷新 env 表——与启动时用的是<b>同一份</b>解析结果。
    /// </summary>
    [RelayCommand]
    public void RefreshPlan()
    {
        EnvRows.Clear();
        PlanError = "";
        HasPlanError = false;

        try
        {
            _plan = _launcher.BuildPlan(Profile.Id, Profile.Profile, dryRun: true);
        }
        catch (RuntimeNotFoundException ex)
        {
            _plan = null;
            PlanError = string.Format(Strings["GameSetting.RuntimeUnavailable"], ex.Detail);
            HasPlanError = true;
            RuntimeExplain = "";
            EnvSummary = "";
            PrefixText = "";
            return;
        }
        catch (Exception ex)
        {
            _plan = null;
            PlanError = ex.Message;
            HasPlanError = true;
            RuntimeExplain = "";
            EnvSummary = "";
            PrefixText = "";
            return;
        }

        foreach (var (key, value) in _plan.Env.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            EnvRows.Add(new GameSettingEnvRow(key, value, _plan.EnvSources.GetValueOrDefault(key, "?")));
        }

        var (headline, note) = BuildRuntimeExplain(_plan);
        RuntimeExplain = headline;
        RuntimeExplainNote = note;
        RuntimeExplainRaw = _plan.Runtime.Explain;
        PrefixText = _plan.Prefix;
        EnvSummary = string.Format(Strings["GameSetting.EnvCount"], EnvRows.Count);
    }


    // ── 右列：外部程序 ────────────────────────────────────────────────

    public ObservableCollection<GameSettingProgramViewModel> Programs { get; } = [];


    [ObservableProperty]
    private string _programStatus = "";


    [ObservableProperty]
    private bool _hasPrograms;


    [ObservableProperty]
    private bool _canStartProgramsNow;


    private void ReloadPrograms()
    {
        Programs.Clear();
        var keywords = BuildKeywords();
        var profile = Profile.Profile;
        foreach (var program in profile.ExternalPrograms)
        {
            Programs.Add(CreateProgramItem(program, keywords));
        }

        HasPrograms = Programs.Count > 0;
        if (!HasPrograms)
        {
            ProgramStatus = "";
        }
    }


    private GameSettingProgramViewModel CreateProgramItem(
        ExternalProgram program,
        IReadOnlyDictionary<string, string?> keywords)
    {
        var item = new GameSettingProgramViewModel(program, keywords, MergeExeKeyword, OnProgramChanged);
        item.StartRequested += OnProgramStartRequested;
        item.CanStartNow = _launcher.State == LaunchPhaseState.Running;
        return item;
    }


    private IReadOnlyDictionary<string, string?> MergeExeKeyword(IReadOnlyDictionary<string, string?> keywords)
    {
        // %exe% 需要跟着"游戏目录里实际存在的 exe"走（用户可能选错目录）
        var definition = GameCatalog.Detect(ProfileConfig.ExpandPath(GamePath));
        return new KeywordReplacer.Builder()
            .Build(keywords.TryGetValue("build", out var build) ? build : null)
            .Prefix(keywords.TryGetValue("prefix", out var prefix) ? prefix : null)
            .Game(keywords.TryGetValue("game", out var game) ? game : null)
            .Launcher(Services.Paths.DataHome)
            .Temp(Path.GetTempPath())
            .Exe(definition?.Executable)
            .Channel(Profile.Profile.Channel)
            .Runtime(keywords.TryGetValue("runtime", out var runtime) ? runtime : null)
            .Build();
    }


    private IReadOnlyDictionary<string, string?> BuildKeywords()
    {
        var gameDirectory = ProfileConfig.ExpandPath(GamePath);
        var definition = GameCatalog.Detect(gameDirectory);
        var prefix = PrefixResolver.Resolve(Profile.Profile.Prefix.ToDomain(), Profile.Id, Services.Paths);

        return new KeywordReplacer.Builder()
            .Game(gameDirectory)
            .Exe(definition?.Executable ?? (Profile.Profile.Game == GameCatalog.Zzz ? "ZenlessZoneZero.exe" : "YuanShen.exe"))
            .Launcher(Services.Paths.DataHome)
            .Prefix(prefix)
            .Temp(Path.GetTempPath())
            .Channel(Profile.Profile.Channel)
            .Build();
    }


    /// <summary>子项改动后：标脏 + 刷新行内派生信息（不落盘，等「应用」）。</summary>
    private void OnProgramChanged()
    {
        SetDirty(true);
        HasPrograms = Programs.Count > 0;
    }


    [ObservableProperty]
    private string? _pendingProgramPath;


    /// <summary>
    /// 「+ 从文件添加」：由视图打开文件选择器，选中后调用本方法。
    /// </summary>
    public void AddProgramFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var isWindowsExe = path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

        var program = new ExternalProgram
        {
            Id = "prog-" + Guid.NewGuid().ToString("N")[..8],
            Name = Path.GetFileNameWithoutExtension(path),
            Path = path,
            Kind = isWindowsExe ? ProgramKind.WindowsExe : ProgramKind.UnixBinary,
            Trigger = Trigger.Manual,
        };

        Profile.Profile.ExternalPrograms.Add(program);
        var item = CreateProgramItem(program, BuildKeywords());
        item.IsExpanded = true;
        Programs.Add(item);
        HasPrograms = true;
        ProgramStatus = string.Format(Strings["GameSetting.ProgramAdded"], program.Name);
        SetDirty(true);
    }


    [RelayCommand]
    private void RemoveProgram(GameSettingProgramViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        Profile.Profile.ExternalPrograms.Remove(item.Model);
        item.StartRequested -= OnProgramStartRequested;
        Programs.Remove(item);
        HasPrograms = Programs.Count > 0;
        ProgramStatus = "";
        SetDirty(true);
    }


    private void OnProgramStartRequested(object? sender, string programId)
    {
        var error = _launcher.StartManualProgram(programId);
        ProgramStatus = error is null
            ? Strings["GameSetting.ProgramStarted"]
            : error;
        if (error is not null)
        {
            Log.Warn($"外部程序启动失败：{error}");
        }
    }


    // ── 右列：诊断 ────────────────────────────────────────────────────

    [ObservableProperty]
    private string _reportSummary = "";


    [ObservableProperty]
    private bool _hasReport;


    [ObservableProperty]
    private string _diagnosticStatus = "";


    /// <summary>launch-report.json 的实际路径（Starward 在诊断卡里也显示路径）。</summary>
    public string DiagnosticReportPath => Services.Paths.LaunchReportFile;


    private void ReloadDiagnostics()
    {
        var path = Services.Paths.LaunchReportFile;
        try
        {
            var report = LaunchReport.ReadFrom(path);
            if (report is null)
            {
                HasReport = false;
                ReportSummary = Strings["GameSetting.DiagnosticEmpty"];
                return;
            }

            HasReport = true;
            ReportSummary = GameSettingDiagnosticText.Summary(report);
        }
        catch (Exception ex)
        {
            HasReport = false;
            ReportSummary = string.Format(Strings["GameSetting.DiagnosticFailed"], ex.Message);
        }
    }


    [RelayCommand]
    private void OpenDiagnosticWindow()
    {
        var report = LaunchReport.ReadFrom(Services.Paths.LaunchReportFile);
        if (report is null)
        {
            DiagnosticStatus = Strings["GameSetting.DiagnosticEmpty"];
            return;
        }

        OpenReport(report);
    }


    private void OpenReport(LaunchReport report)
    {
        var window = new Views.DiagnosticWindow
        {
            DataContext = new ViewModels.DiagnosticViewModel(report, Services.Paths),
        };
        window.Show();
        DiagnosticStatus = "";
    }


    [RelayCommand]
    private void OpenEnvironmentShell()
    {
        try
        {
            var plan = _plan ?? _launcher.BuildPlan(Profile.Id, Profile.Profile, dryRun: true);
            var shell = new Views.ShellWindow
            {
                DataContext = new ViewModels.EnvironmentShellViewModel(plan, Services.Paths),
            };
            shell.Show();
            DiagnosticStatus = "";
        }
        catch (Exception ex)
        {
            DiagnosticStatus = ex.Message;
        }
    }


    /// <summary>
    /// 「粘贴 launch-report 分析」：把别的机器/别人的报告贴进来解析（离线排障）。
    /// </summary>
    /// <param name="json">剪贴板文本；由视图层通过 <c>TopLevel.Clipboard</c> 取。</param>
    public void AnalyzePastedReport(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            DiagnosticStatus = Strings["GameSetting.PasteEmpty"];
            return;
        }

        try
        {
            var report = JsonSerializer.Deserialize<LaunchReport>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (report is null)
            {
                DiagnosticStatus = Strings["GameSetting.PasteInvalid"];
                return;
            }

            OpenReport(report);
        }
        catch (JsonException ex)
        {
            DiagnosticStatus = string.Format(Strings["GameSetting.PasteInvalidDetail"], ex.Message);
        }
    }


    // ── 应用条 ────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isDirty;


    [ObservableProperty]
    private string _applyHint = "";


    private void SetDirty(bool dirty)
    {
        IsDirty = dirty;
        ApplyHint = dirty
            ? Strings["GameSetting.ApplyHintDirty"]
            : Strings["GameSetting.ApplyHintClean"];
    }


    [RelayCommand]
    private void Apply()
    {
        try
        {
            var profile = Profile.Profile;
            // GamePath / 各选项的 setter 已经写进 profile，这里只做边界收敛与落盘
            profile.ReadyGraceMs = Math.Clamp((int)(ReadyGraceMsValue ?? 6000), 0, 600_000);
            profile.Channel = SelectedChannel?.Key ?? "Official";
            profile.Wine.Sync = SelectedSync?.Value ?? WineSync.None;
            profile.Enhancements.Hud = SelectedHud?.Value ?? "None";

            Services.Config.Profiles[Profile.Id] = profile;
            Services.Config.Save(Services.Paths.ConfigFile);

            SetDirty(false);
            PlanError = "";
            HasPlanError = false;
            Log.Info($"已保存游戏设置：{Profile.Id}");
            RefreshPlan();
            ReloadDiagnostics();
        }
        catch (Exception ex)
        {
            PlanError = ex.Message;
            HasPlanError = true;
            Log.Error("保存游戏设置失败", ex);
        }
    }


    // ── 相位 ──────────────────────────────────────────────────────────

    private void OnLauncherStateChanged(object? sender, LaunchPhaseState state)
        => Dispatcher.UIThread.Post(() => ApplyLauncherState(state));


    private void ApplyLauncherState(LaunchPhaseState state)
    {
        var running = state == LaunchPhaseState.Running;
        CanStartProgramsNow = running;
        foreach (var program in Programs)
        {
            program.CanStartNow = running;
        }
    }


    // ── 页面生命周期 ──────────────────────────────────────────────────

    /// <summary>页面重新挂载时刷新（可能刚启动过游戏，报告/相位都变了）。</summary>
    public void OnNavigatedTo()
    {
        ApplyLauncherState(_launcher.State);
        ReloadDiagnostics();
        RefreshChannelCheck();
    }


    private static GameEdition EditionOf(ProfileConfig profile)
        => string.Equals(profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
            ? GameEdition.Global
            : GameEdition.China;


    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _launcher.StateChanged -= OnLauncherStateChanged;
        foreach (var program in Programs)
        {
            program.StartRequested -= OnProgramStartRequested;
        }
    }
}


/// <summary>扫描到的一个游戏目录（游戏设置页"游戏目录"下的列表项）。</summary>
public sealed partial class DetectedGameRow : ObservableObject
{
    private readonly IRelayCommand<DetectedGameRow?> _useCommand;


    public DetectedGameRow(DetectedGame game, IRelayCommand<DetectedGameRow?> useCommand)
    {
        Definition = game.Definition;
        Directory = game.Directory;
        Source = game.Source;
        _useCommand = useCommand;
    }


    public GameDefinition Definition { get; }

    public string Directory { get; }

    public string Source { get; }

    public string DisplayName => $"{Definition.DisplayName} · {(Definition.Edition == GameEdition.Global ? "国际服" : "国服")}";

    /// <summary>只显示最后两级目录，长路径交给 ToolTip（避免行内省略号截掉关键信息）。</summary>
    public string ShortDirectory
    {
        get
        {
            var parts = Directory.TrimEnd('/').Split('/');
            return parts.Length <= 2
                ? Directory
                : "…/" + string.Join('/', parts[^2..]);
        }
    }


    [ObservableProperty]
    private bool _isCurrent;


    [ObservableProperty]
    private bool _isForCurrentGame;


    public void Refresh(string currentDirectory, string currentKind, string currentEdition)
    {
        IsCurrent = string.Equals(
            Directory.TrimEnd('/'),
            currentDirectory.TrimEnd('/'),
            StringComparison.Ordinal);
        IsForCurrentGame = string.Equals(Definition.Kind, currentKind, StringComparison.OrdinalIgnoreCase)
                           && Definition.Edition == (string.Equals(currentEdition, "Global", StringComparison.OrdinalIgnoreCase)
                               ? GameEdition.Global
                               : GameEdition.China);
        OnPropertyChanged(nameof(ShortDirectory));
    }


    [RelayCommand]
    private void Use() => _useCommand.Execute(this);
}


/// <summary>
/// 诊断摘要文案（launch-report.json 的一行结论）。
/// </summary>
/// <remarks>
/// <see cref="ViewModels.DiagnosticViewModel"/> 是旧窗口的视图模型，改动它会牵动别的页面；
/// 游戏设置页只需要一行摘要，因此把格式化放在本页自己的文件里。
/// </remarks>
internal static class GameSettingDiagnosticText
{
    public static string Summary(LaunchReport report)
    {
        var strings = AppStrings.Instance;
        var parts = new List<string>
        {
            report.Success ? strings["GameSetting.DiagnosticSuccess"] : strings["GameSetting.DiagnosticFailure"],
            string.Format(strings["GameSetting.DiagnosticExitCode"], report.ExitCode),
        };

        if (!string.IsNullOrEmpty(report.ReadySignal))
        {
            parts.Add(string.Format(strings["GameSetting.DiagnosticSignal"], report.ReadySignal));
        }

        if (report.GamePid is { } pid)
        {
            parts.Add($"pid {pid}");
        }

        parts.Add(report.Timestamp.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.CurrentCulture));
        return string.Join(" · ", parts);
    }
}
