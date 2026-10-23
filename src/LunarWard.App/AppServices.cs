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
        LauncherService = new Services.LauncherService(this);
        Background = new Services.BackgroundService(paths);
        Announcements = new Services.AnnouncementService(paths);
        Scanner = new Services.GameScanner(paths);
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


    public static void Initialize(IXdgPaths paths)
    {
        Current = new AppServices(paths);
        (paths as XdgPaths)?.EnsureDirectories();
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
