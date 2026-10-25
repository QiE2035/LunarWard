using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using LunarWard.Platform;

namespace LunarWard.App;

/// <summary>
/// 启动器运行时服务容器。刻意保持极简（构造器注入 + 单例），
/// 不引入 <c>Microsoft.Extensions.DependencyInjection</c> 以免多一层配置。
/// </summary>
public sealed class AppServices
{
    public static AppServices Current { get; private set; } = null!;


    public AppServices(IXdgPaths paths)
    {
        Paths = paths;
        Processes = new LinuxProcessTree();
        Controller = new LinuxProcessController();
        Launcher = new UnixProcessLauncher();
        PrefixManager = new Wine.PrefixManager(Processes, Controller, Launcher, paths);
        Components = new Wine.ComponentRepository(paths.ComponentsDirectory);
        RunnerResolver = new Wine.RunnerResolver(paths, Components);
        Dxvk = new Wine.DxvkManager();
        Config = Launch.LauncherConfig.Load(paths.ConfigFile, out var warning);
        ConfigWarning = warning;
        Log = new Services.LogService(paths);
        LauncherService = new Services.LauncherService(this);
        Background = new Services.BackgroundService(paths);
        // 旧版 accounts.json 一次性导入 GameAccount 表（上游没有这个文件，属于兼容迁移）；
        // 截图自检用临时库，不迁移用户账号。
        if (!CaptureMode.Enabled)
        {
            try
            {
                new LunarWard.Games.GameAccountStore(new LunarWard.Launch.Database.LunarWardDatabase(
                    System.IO.Path.Join(paths.DataHome, LunarWard.Launch.Database.LunarWardDatabase.FileName)))
                    .MigrateLegacyJson(paths.DataHome);
            }
            catch (Exception)
            {
                // 迁移失败不影响启动
            }
        }

        Announcements = new Services.AnnouncementService(paths);
        Scanner = new Services.GameScanner(paths);
        // 自检截图用临时库，避免在用户的 LunarWardDatabase.db 里建表/写时长
        PlayTime = CaptureMode.Enabled
            ? new Launch.PlayTimeService(paths, new Launch.Database.PlayTimeStore(CaptureMode.CreateDatabase()))
            : new Launch.PlayTimeService(paths);
        ComponentInstaller = new Wine.ComponentInstaller(paths);
        Localization.AppStrings.Instance.Language = Localization.AppStrings.Normalize(Config.Launcher.Language);
        Installer = new LunarWard.Installer.GameInstallController(new Services.InstallerLogAdapter(Log));

        // 把"游戏安装路径"的来源接到 Wine：LunarWard.Core 的抽卡模块靠它定位 webCaches。
        // 没装 wine prefix 时保持 null（Linux 无注册表）。
        GlobalGameInstallPathProvider.Configure(this);
    }


    public IXdgPaths Paths { get; }

    public LinuxProcessTree Processes { get; }

    public LinuxProcessController Controller { get; }

    public UnixProcessLauncher Launcher { get; }

    public Wine.PrefixManager PrefixManager { get; }

    public Wine.ComponentRepository Components { get; }

    public Wine.RunnerResolver RunnerResolver { get; }

    public Wine.DxvkManager Dxvk { get; }

    public Launch.LauncherConfig Config { get; }

    public string? ConfigWarning { get; }

    public Services.LauncherService LauncherService { get; }

    public Services.BackgroundService Background { get; }

    public Services.AnnouncementService Announcements { get; }

    public Services.GameScanner Scanner { get; }

    public Launch.PlayTimeService PlayTime { get; }

    public Wine.ComponentInstaller ComponentInstaller { get; }

    public Services.LogService Log { get; }

    /// <summary>内嵌安装器的公开门面（Starward 里由 RPC 服务提供，这里直接持有）。</summary>
    public LunarWard.Installer.GameInstallController Installer { get; }

    /// <summary>应用退出前调用：停掉所有安装任务并释放安装器。</summary>
    public void ShutdownInstaller()
    {
        try
        {
            Installer.StopAllTasks("App shutdown");
            Installer.Dispose();
        }
        catch
        {
            // 退出路径上不抛异常
        }
    }


    public static void Initialize(IXdgPaths paths)
    {
        Current = new AppServices(paths);
        (paths as XdgPaths)?.EnsureDirectories();
    }


    /// <summary>
    /// 抽卡/便笺模块需要的"游戏安装路径"。
    /// </summary>
    /// <remarks>
    /// <c>GachaLogClient.InstallPathProvider</c> 是静态属性（上游也是静态注册表读取），
    /// 这里集中一处设置，避免散落在各处。
    /// </remarks>
    private static class GlobalGameInstallPathProvider
    {
        public static void Configure(AppServices services)
        {
            LunarWard.Core.Gacha.GachaLogClient.InstallPathProvider = new Provider(services);
        }


        private sealed class Provider : LunarWard.Core.IGameInstallPathProvider
        {
            private readonly AppServices _services;

            public Provider(AppServices services) => _services = services;


            public string? GetInstallPath(LunarWard.Core.GameBiz gameBiz)
            {
                // 1) 优先用当前档案里配的游戏目录
                foreach (var profile in _services.Config.Profiles.Values)
                {
                    var dir = Launch.ProfileConfig.ExpandPath(profile.Path);
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    {
                        continue;
                    }

                    var definition = Games.GameCatalog.Detect(dir);
                    if (definition is not null && definition.GameBiz == gameBiz.Value)
                    {
                        return dir;
                    }
                }

                // 2) 退到 Wine prefix 的 user.reg（游戏自己写过 InstallPath）
                foreach (var profile in _services.Config.Profiles.Values)
                {
                    var prefix = Launch.PrefixResolver.Resolve(profile.Prefix.ToDomain(), profile.Game, _services.Paths);
                    if (!Directory.Exists(prefix))
                    {
                        continue;
                    }

                    var registry = new Games.WineRegistryInstallPathProvider(prefix);
                    var path = registry.GetInstallPath(gameBiz);
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    {
                        return path;
                    }
                }

                return null;
            }


            public string? GetLauncherPath(string registryKey) => null;
        }
    }


    /// <summary>构建一个 profile 的 <see cref="Wine.RuntimeResolver"/>（每次读最新配置）。</summary>
    public Wine.RuntimeResolver CreateRuntimeResolver()
    {
        var runtimes = Config.Runtimes.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToDomain(),
            StringComparer.Ordinal);

        return new Wine.RuntimeResolver(runtimes, Config.Launcher.DefaultRuntime, RunnerResolver, Components, Paths);
    }


    public Launch.LaunchPlanFactory CreatePlanFactory() => new(CreateRuntimeResolver(), Paths);


    /// <summary>用系统默认浏览器打开链接（公告/资讯/帮助）。失败只记日志，不抛。</summary>
    public Task OpenUriAsync(string uri)
    {
        try
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            {
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = parsed.AbsoluteUri,
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception)
        {
            // 没有默认浏览器：静默失败
        }

        return Task.CompletedTask;
    }
}


/// <summary>
/// Acrylic 分级策略（L2 半透明 / L3 纯色）。
/// </summary>
/// <remarks>
/// 见 <c>docs/04-FEATURES.md</c> §5.6：只实现 L2 + L3，不做 KDE 私有的 L1 真模糊。
/// </remarks>
public static class AcrylicLevel
{
    /// <summary>本机探测到的级别。</summary>
    public static AcrylicLevelKind Detect(TopLevel? topLevel)
    {
        if (topLevel is null)
        {
            return AcrylicLevelKind.Solid;
        }

        try
        {
            // Avalonia 12 的 WindowTransparencyLevel 是普通枚举，
            // 不支持复合的合成器会回落到 None。
            var actual = topLevel.ActualTransparencyLevel;
            return actual.ToString() == "None"
                ? AcrylicLevelKind.Solid
                : AcrylicLevelKind.Translucent;
        }
        catch (Exception)
        {
            return AcrylicLevelKind.Solid;
        }
    }


    /// <summary>L2 的半透明渐变层。</summary>
    public static IBrush TranslucentBrush { get; } = new ImmutableLinearGradientBrush(
        new[]
        {
            new ImmutableGradientStop(0.0, Color.Parse("#CC101018")),
            new ImmutableGradientStop(0.55, Color.Parse("#B0182030")),
            new ImmutableGradientStop(1.0, Color.Parse("#D9080C14")),
        },
        startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
        endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));


    /// <summary>L3 的纯色兜底。</summary>
    public static IBrush SolidBrush { get; } = new ImmutableSolidColorBrush(Color.Parse("#101318"));


    public static IBrush BrushFor(AcrylicLevelKind kind)
        => kind == AcrylicLevelKind.Solid ? SolidBrush : TranslucentBrush;
}


public enum AcrylicLevelKind
{
    /// <summary>L2：半透明渐变。</summary>
    Translucent,

    /// <summary>L3：不透明纯色。</summary>
    Solid,
}
