using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.App.ViewModels;
using LunarWard.App.Views.Starward;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// Starward 外壳（对应 Starward 的 <c>MainView</c>）：左栏导航、游戏选择、背景、
/// 页面 Frame、公告 Toast。
/// </summary>
/// <remarks>
/// <para>
/// 与旧 <see cref="MainWindowViewModel"/> 的分工：外壳只管"导航 + 当前游戏 + 背景"，
/// 启动逻辑仍由 <see cref="Services.LauncherService"/> 提供，启动器页自己的 VM 订阅它。
/// 切换游戏时外壳重建页面（<see cref="PageChanged"/>），页面的 VM 因此可以安全地
/// 在构造时缓存 profile，不需要处理"profile 变了"。
/// </para>
/// <para>布局与像素契约见 <c>docs/07-UI-REPLICA.md</c> §1–§2。</para>
/// </remarks>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;

    private readonly LogService _log;

    private bool _disposed;


    public ShellViewModel(AppServices services, LogService log)
    {
        _services = services;
        _log = log;
        Assets = new StarwardAssets(services.Paths, log);

        Profiles = new ObservableCollection<ProfileItemViewModel>();
        ReloadProfiles();

        CurrentPage = ShellPage.Launcher;
        _ = InitializeAsync();
    }


    // ── 服务 ──────────────────────────────────────────────────────────

    public AppServices Services => _services;

    public StarwardAssets Assets { get; }

    public LogService Log => _log;

    public Localization.AppStrings Strings => Localization.AppStrings.Instance;

    /// <summary>当前游戏档案。</summary>
    [ObservableProperty]
    private ProfileItemViewModel _profile = null!;

    public ObservableCollection<ProfileItemViewModel> Profiles { get; }

    /// <summary>游戏选择层的条目（含未安装的游戏）。</summary>
    public ObservableCollection<GameChoiceViewModel> GameChoices { get; } = [];


    // ── 导航 ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLauncherSelected), nameof(IsGameSettingSelected), nameof(IsScreenshotSelected),
        nameof(IsGachaSelected), nameof(IsToolboxSelected), nameof(IsSettingsSelected))]
    private ShellPage _currentPage;


    public bool IsLauncherSelected => CurrentPage == ShellPage.Launcher;

    public bool IsGameSettingSelected => CurrentPage == ShellPage.GameSetting;

    public bool IsScreenshotSelected => CurrentPage == ShellPage.Screenshot;

    public bool IsGachaSelected => CurrentPage == ShellPage.Gacha;

    public bool IsToolboxSelected => CurrentPage == ShellPage.Toolbox;

    public bool IsSettingsSelected => CurrentPage == ShellPage.Settings;


    /// <summary>页面容器重建通知（参数是新页面的 <see cref="PageContext"/>）。</summary>
    public event EventHandler<PageContext>? PageChanged;

    /// <summary>游戏切换通知（背景、公告卡等需要跟着换）。</summary>
    public event EventHandler? GameChanged;

    public event EventHandler? GameSelectorVisibilityChanged;


    [RelayCommand]
    private void SelectPage(ShellPage page)
    {
        if (CurrentPage == page)
        {
            return;
        }

        CurrentPage = page;
        RebuildPage();
    }


    /// <summary>由页面主动请求跳转（例如设置页里的"诊断"跳到工具箱）。</summary>
    public void Navigate(ShellPage page) => SelectPageCommand.Execute(page);


    public void RebuildPage() => PageChanged?.Invoke(this, new PageContext
    {
        Services = _services,
        Shell = this,
        Profile = Profile,
        Log = _log,
    });


    // ── 游戏选择层 ────────────────────────────────────────────────────

    [ObservableProperty]
    private bool isGameSelectorOpen;

    partial void OnIsGameSelectorOpenChanged(bool value) => GameSelectorVisibilityChanged?.Invoke(this, EventArgs.Empty);


    [RelayCommand]
    private void OpenGameSelector()
    {
        BuildGameChoices();
        IsGameSelectorOpen = true;
    }


    [RelayCommand]
    private void CloseGameSelector() => IsGameSelectorOpen = false;


    /// <summary>选择一个档案（或为未安装的游戏新建档案）。</summary>
    public async Task ChooseGameAsync(GameChoiceViewModel choice)
    {
        IsGameSelectorOpen = false;
        if (choice.ProfileId is { } id && Profiles.FirstOrDefault(x => x.Id == id) is { } existing)
        {
            SelectProfile(existing);
            return;
        }

        if (choice.Definition is not { } definition)
        {
            return;
        }

        // 未安装：建一个空路径的档案，用户在游戏设置页里指定目录
        var profileId = $"{definition.Kind}-{(definition.Edition == Games.GameEdition.Global ? "global" : "cn")}";
        if (!_services.Config.Profiles.TryGetValue(profileId, out var profile))
        {
            profile = new ProfileConfig
            {
                Game = definition.Kind,
                Edition = definition.Edition == Games.GameEdition.Global ? "Global" : "China",
                Path = "",
                Channel = "Official",
            };
            _services.Config.Profiles[profileId] = profile;
            SaveConfig();
        }

        ReloadProfiles();
        if (Profiles.FirstOrDefault(x => x.Id == profileId) is { } created)
        {
            SelectProfile(created);
        }

        await Task.CompletedTask;
    }


    /// <summary>按 id 选中档案（自检 <c>--profile</c> 与 URL 协议共用）。</summary>
    public void SelectProfileById(string id)
    {
        if (Profiles.FirstOrDefault(x => x.Id == id) is { } item)
        {
            SelectProfile(item);
        }
    }


    private void SelectProfile(ProfileItemViewModel item)
    {
        if (ReferenceEquals(Profile, item))
        {
            return;
        }

        Profile = item;
        _services.Config.Launcher.LastProfileIndex = Profiles.IndexOf(item);
        SaveConfig();

        _ = LoadArtAsync();
        GameChanged?.Invoke(this, EventArgs.Empty);
        RebuildPage();
    }


    // ── 图标与背景 ────────────────────────────────────────────────────

    [ObservableProperty]
    private Bitmap? _currentGameIcon;

    public bool HasGameIcon => CurrentGameIcon is not null;

    partial void OnCurrentGameIconChanged(Bitmap? value) => OnPropertyChanged(nameof(HasGameIcon));


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackgroundImage))]
    private Bitmap? _backgroundImage;

    public bool HasBackgroundImage => BackgroundImage is not null;


    [ObservableProperty]
    private bool _isBackgroundLoading;


    /// <summary>当前游戏的 biz（<c>hk4e_cn</c> 等），供素材/公告/便笺使用。</summary>
    public string CurrentGameBiz => Games.GameCatalog.Get(
        Profile.Profile.Game,
        string.Equals(Profile.Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
            ? Games.GameEdition.Global
            : Games.GameEdition.China)?.GameBiz ?? "hk4e_cn";


    /// <summary>当前游戏的 launcher id（官方/ B 站）。</summary>
    public string CurrentLauncherId => Core.HoYoPlay.LauncherId.FromGameBiz(new Core.GameBiz(CurrentGameBiz))
                                       ?? Core.HoYoPlay.LauncherId.ChinaOfficial;


    // ── 生命周期 ──────────────────────────────────────────────────────

    private async Task InitializeAsync()
    {
        // 背景先用本地自定义图，再用官方素材覆盖（Starward 也是本地优先）
        ShowLocalBackground();
        await LoadArtAsync().ConfigureAwait(true);
    }


    private async Task LoadArtAsync()
    {
        try
        {
            IsBackgroundLoading = true;
            await Assets.LoadAsync(CurrentLauncherId, CurrentGameBiz).ConfigureAwait(true);
            var art = Assets.For();

            if (await Assets.BitmapAsync(art?.Icon).ConfigureAwait(true) is { } icon)
            {
                CurrentGameIcon = icon;
            }

            if (_videoBackground is null && await Assets.BitmapAsync(art?.Background).ConfigureAwait(true) is { } background)
            {
                BackgroundImage = background;
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"素材加载失败：{ex.Message}");
        }
        finally
        {
            IsBackgroundLoading = false;
            GameChanged?.Invoke(this, EventArgs.Empty);
        }
    }


    /// <summary>本地背景（<c>~/.cache/lunarward/background</c>），存在时优先于官方图。</summary>
    private void ShowLocalBackground()
    {
        try
        {
            var items = _services.Background.Discover();
            var video = items.FirstOrDefault(x => x.IsVideo);
            var image = items.FirstOrDefault(x => x.ImagePath is not null);
            if (video is not null && _services.Background.IsVlcAvailable)
            {
                _videoBackground = video.VideoPath;
                VideoBackgroundRequested?.Invoke(this, video.VideoPath!);
                return;
            }

            if (image is not null)
            {
                BackgroundImage = new Bitmap(image.ImagePath!);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"本地背景加载失败：{ex.Message}");
        }
    }


    private string? _videoBackground;

    /// <summary>视图收到后把 VideoView 挂到背景层。</summary>
    public event EventHandler<string>? VideoBackgroundRequested;

    public string? VideoBackgroundPath => _videoBackground;


    // ── 档案 ──────────────────────────────────────────────────────────

    public void ReloadProfiles()
    {
        var previousId = Profile?.Id;
        Profiles.Clear();

        // Starward 的网格选择器列出所有游戏；已安装的给出 profile
        foreach (var definition in Games.GameCatalog.All)
        {
            var existing = _services.Config.Profiles.FirstOrDefault(kv =>
                kv.Value.Game == definition.Kind
                && string.Equals(kv.Value.Edition, definition.Edition == Games.GameEdition.Global ? "Global" : "China",
                    StringComparison.OrdinalIgnoreCase));

            var isInstalled = existing.Key is not null && Directory.Exists(ProfileConfig.ExpandPath(existing.Value.Path));
            Profiles.Add(new ProfileItemViewModel(
                existing.Key ?? $"{definition.Kind}-{(definition.Edition == Games.GameEdition.Global ? "global" : "cn")}",
                existing.Value ?? new ProfileConfig { Game = definition.Kind, Path = "", Edition = definition.Edition == Games.GameEdition.Global ? "Global" : "China", Channel = "Official" },
                definition.DisplayName)
            {
                IsInstalled = isInstalled,
            });
        }

        EnsureDefaultRuntime();

        var index = Math.Clamp(_services.Config.Launcher.LastProfileIndex, 0, Math.Max(0, Profiles.Count - 1));
        Profile = Profiles.FirstOrDefault(p => p.Id == previousId) ?? Profiles[index];

        if (!ReferenceEquals(Profile, null))
        {
            _services.Config.Launcher.LastProfileIndex = Profiles.IndexOf(Profile);
        }
    }


    private void BuildGameChoices()
    {
        GameChoices.Clear();
        foreach (var profile in Profiles)
        {
            var definition = Games.GameCatalog.Detect(ProfileConfig.ExpandPath(profile.Profile.Path));
            GameChoices.Add(new GameChoiceViewModel(profile, definition));
        }
    }


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

        SaveConfig();
    }


    private void SaveConfig()
    {
        try
        {
            _services.Config.Save(_services.Paths.ConfigFile);
        }
        catch (Exception ex)
        {
            _log.Error("保存配置失败", ex);
        }
    }


    public void Shutdown() => _services.LauncherService.Shutdown();


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Assets.Dispose();
    }
}


/// <summary>游戏选择层的一格（Starward GameSelector 的 192×108 卡片）。</summary>
public sealed class GameChoiceViewModel
{
    public GameChoiceViewModel(ProfileItemViewModel profile, Games.GameDefinition? definition)
    {
        ProfileId = profile.IsInstalled ? profile.Id : null;
        Definition = definition;
        Name = profile.DisplayName;
        Edition = definition?.Edition == Games.GameEdition.Global ? "国际服" : "国服";
        IsInstalled = profile.IsInstalled;
        IsCurrent = false;
    }


    public string? ProfileId { get; }

    public Games.GameDefinition? Definition { get; }

    public string Name { get; }

    public string Edition { get; }

    public bool IsInstalled { get; }

    public bool IsCurrent { get; set; }

    public string Title => $"{Name} · {Edition}";
}
