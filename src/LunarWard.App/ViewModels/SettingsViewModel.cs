using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.Games;
using LunarWard.Launch;
using LunarWard.Wine;

namespace LunarWard.App.ViewModels;

/// <summary>
/// 设置窗口：游戏路径扫描、运行时选择、渠道、wine 选项。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;

    private readonly ProfileItemViewModel _profile;


    public SettingsViewModel(AppServices services, ProfileItemViewModel profile)
    {
        _services = services;
        _profile = profile;

        Runtimes = new ObservableCollection<AvailableRuntime>(new RuntimeScanner(services.Paths).Scan());
        DetectedGames = new ObservableCollection<DetectedGame>(new GameScanner(services.Paths).Scan());

        SelectedRuntimeName = services.Config.Launcher.DefaultRuntime;
        SyncSelection = profile.Profile.Wine.Sync;
        LoadFromProfile();
    }


    public ProfileItemViewModel Profile => _profile;

    public ObservableCollection<AvailableRuntime> Runtimes { get; }

    public ObservableCollection<DetectedGame> DetectedGames { get; }

    public IReadOnlyList<WineSync> SyncOptions { get; } = Enum.GetValues<WineSync>();

    public IReadOnlyList<string> Channels { get; } = ["Official", "Bilibili"];

    public IReadOnlyList<string> Editions { get; } = ["China", "Global"];

    public IReadOnlyList<string> Kinds { get; } = [GameCatalog.Genshin, GameCatalog.Zzz];


    [ObservableProperty]
    private string gamePath = "";


    [ObservableProperty]
    private string kind = GameCatalog.Genshin;


    [ObservableProperty]
    private string edition = "China";


    [ObservableProperty]
    private string channel = "Official";


    [ObservableProperty]
    private AvailableRuntime? selectedRuntime;


    [ObservableProperty]
    private string? selectedRuntimeName;


    [ObservableProperty]
    private WineSync syncSelection;


    [ObservableProperty]
    private bool borderless;


    [ObservableProperty]
    private bool wineWayland;


    [ObservableProperty]
    private bool timeoutFix;


    [ObservableProperty]
    private bool fsr;


    [ObservableProperty]
    private bool dx12;


    [ObservableProperty]
    private string hud = "None";


    [ObservableProperty]
    private int readyGraceMs = 6000;


    [ObservableProperty]
    private string? message;


    public IReadOnlyList<string> HudOptions { get; } = ["None", "Fps", "Full", "fps,frametimes,devinfo,memory,gpuload"];


    private void LoadFromProfile()
    {
        var profile = _profile.Profile;
        GamePath = profile.Path;
        Kind = profile.Game;
        Edition = profile.Edition;
        Channel = profile.Channel;
        Borderless = profile.Wine.Borderless;
        WineWayland = profile.Wine.WineWayland;
        TimeoutFix = profile.Wine.TimeoutFix;
        Fsr = profile.Enhancements.Fsr;
        Dx12 = profile.Enhancements.Dx12;
        Hud = profile.Enhancements.Hud;
        ReadyGraceMs = profile.ReadyGraceMs;
        SyncSelection = profile.Wine.Sync;
        SelectedRuntime = Runtimes.FirstOrDefault(r => r.Name == SelectedRuntimeName);
    }


    partial void OnSelectedRuntimeChanged(AvailableRuntime? value)
    {
        if (value is null)
        {
            return;
        }

        _services.Config.Launcher.DefaultRuntime = value.Name;
        SelectedRuntimeName = value.Name;
        Message = $"默认运行时：{value.Name}";
    }


    partial void OnKindChanged(string value)
    {
        // ZZZ 没有 B 站渠道：自动回落到官方
        if (string.Equals(value, GameCatalog.Zzz, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Channel, "Bilibili", StringComparison.Ordinal))
        {
            Channel = "Official";
            Message = "绝区零没有 B 站渠道，已切回官方";
        }
    }


    [RelayCommand]
    private void ScanGames()
    {
        var found = new GameScanner(_services.Paths).Scan();
        DetectedGames.Clear();
        foreach (var game in found)
        {
            DetectedGames.Add(game);
        }

        Message = found.Count == 0
            ? "没有扫描到游戏目录（可手动填写路径）"
            : $"扫描到 {found.Count} 个游戏目录";
    }


    [RelayCommand]
    private void ScanRuntimes()
    {
        var found = new RuntimeScanner(_services.Paths).Scan();
        Runtimes.Clear();
        foreach (var runtime in found)
        {
            Runtimes.Add(runtime);
        }

        Message = found.Count == 0 ? "没有找到可用的 wine" : $"找到 {found.Count} 个运行时";
    }


    [RelayCommand]
    private void UseDetectedGame(DetectedGame? game)
    {
        if (game is null)
        {
            return;
        }

        GamePath = game.Directory;
        Kind = game.Definition.Kind;
        Edition = game.Definition.Edition.ToString();
        Message = $"已选择 {game.Definition.DisplayName}：{game.Directory}";
    }


    [RelayCommand]
    private void Save() => Apply();


    /// <summary>写回 profile 并保存配置。</summary>
    public void Apply()
    {
        var profile = _profile.Profile;
        profile.Game = Kind;
        profile.Edition = Edition;
        profile.Path = GamePath;
        profile.Channel = Channel;
        profile.Wine.Borderless = Borderless;
        profile.Wine.WineWayland = WineWayland;
        profile.Wine.TimeoutFix = TimeoutFix;
        profile.Wine.Sync = SyncSelection;
        profile.Enhancements.Fsr = Fsr;
        profile.Enhancements.Dx12 = Dx12;
        profile.Enhancements.Hud = Hud;
        profile.ReadyGraceMs = Math.Clamp(ReadyGraceMs, 0, 600_000);

        if (SelectedRuntime is { } runtime)
        {
            // 选中的运行时写进 runtimes 表，再以 Shared 引用它，
            // 这样"生效运行时"在 UI 里可解释（docs/04-FEATURES.md §3.1）。
            var config = _services.Config.Runtimes.TryGetValue(runtime.Name, out var existing)
                ? existing
                : new RuntimeConfigDto();

            config.Runner = RunnerDto.FromDomain(runtime.Runner);
            _services.Config.Runtimes[runtime.Name] = config;
            profile.Runtime = new ScopedRuntimeDto { Kind = "Shared", Name = runtime.Name };
            _services.Config.Launcher.DefaultRuntime = runtime.Name;
        }

        _services.Config.Profiles[_profile.Id] = profile;
        _services.Config.Save(_services.Paths.ConfigFile);
    }
}
