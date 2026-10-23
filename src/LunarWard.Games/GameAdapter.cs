using LunarWard.Wine;

namespace LunarWard.Games;

/// <summary>
/// 游戏适配器（层 8）的输出：游戏特化的环境变量与启动参数。
/// </summary>
/// <remarks>
/// 层号见 <c>docs/03-LAUNCH-ENGINE.md</c> §6：层 8 在 <c>profile.env</c>（层 7）之上、
/// 外部程序 env（层 9）之下。
/// </remarks>
public sealed class GameAdapterEnvResult
{
    /// <summary>层 8：游戏特化 env。空串是合法值（<c>DISPLAY=""</c>）。</summary>
    public Dictionary<string, string> Env { get; init; } = new(StringComparer.Ordinal);

    /// <summary>游戏特化启动参数，例如 borderless 的 <c>-screen-fullscreen 0 -popupwindow</c>。</summary>
    public List<string> ExtraArguments { get; init; } = [];

    /// <summary>这些值的来源层名（用于 <c>EnvSources</c>）。恒为 <c>game</c>。</summary>
    public string Layer => EnvLayer.GameAdapter;


    /// <summary>
    /// 合并到一个 <see cref="EnvironmentBuilder"/>（层 8）。
    /// </summary>
    public EnvironmentBuilder ApplyTo(EnvironmentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Apply(EnvLayer.GameAdapter, Env);
    }
}


/// <summary>
/// 游戏特化适配（层 8）。<b>纯静态、无 IO</b>，可直接单元测试。
/// </summary>
/// <remarks>
/// <para>
/// 取值来源是 <c>anime-launcher-sdk</c> 的 <c>games/*/game.rs</c>（实测参考实现）：
/// </para>
/// <list type="bullet">
/// <item><c>timeout_fix</c> → <c>WINE_ENABLE_TIMEOUT_FIX=1</c></item>
/// <item><c>winewayland</c> → <c>DISPLAY=""</c>（<b>存在且为空</b>，不是删除 —— 见 D7）</item>
/// <item><c>borderless</c> → <c>-screen-fullscreen 0 -popupwindow</c></item>
/// <item>ZZZ + <c>dx12</c> → <c>DXVK_ENABLE_NVAPI=1</c> + <c>VKD3D_CONFIG=dxr</c></item>
/// <item><c>fsr</c> → <c>WINE_FULLSCREEN_FSR=1</c> + <c>WINE_FULLSCREEN_FSR_STRENGTH=2</c></item>
/// </list>
/// <para>
/// <b>注意</b>：<c>DISPLAY</c> 用空串而<b>不是</b>移除。对 wine 而言"变量存在但为空"
/// 与"不存在"是不同行为；<c>Remove</c> 会让 wine 退回 X11。见
/// <c>docs/00-DECISIONS.md</c> D7 与 <c>docs/04-FEATURES.md</c> §1.4。
/// </para>
/// </remarks>
public static class GameAdapter
{
    /// <summary>winewayland 模式下 <c>DISPLAY</c> 的值（空串，不是删除）。</summary>
    public const string WaylandDisplayValue = "";

    /// <summary>FSR 强度默认值（参考实现的默认）。</summary>
    public const int DefaultFsrStrength = 2;

    /// <summary>borderless 的启动参数。</summary>
    public static IReadOnlyList<string> BorderlessArguments { get; } =
        ["-screen-fullscreen", "0", "-popupwindow"];

    /// <summary>DX12 的启动参数（仅 ZZZ）。</summary>
    public static IReadOnlyList<string> Dx12Arguments { get; } = ["-use-d3d12"];

    /// <summary>FSR 需要的窗口模式参数（参考实现：FSR 只在独占全屏下生效）。</summary>
    public static IReadOnlyList<string> FsrExclusiveWindowArguments { get; } = ["-window-mode", "exclusive"];


    /// <summary>
    /// 构建层 8 的游戏特化 env 与启动参数。
    /// </summary>
    /// <param name="gameKind"><c>genshin</c> | <c>zzz</c>（大小写不敏感；未知按一般游戏处理）。</param>
    /// <param name="wineWayland">winewayland 模式 → <c>DISPLAY=""</c>。</param>
    /// <param name="timeoutFix">补丁 <c>WINE_ENABLE_TIMEOUT_FIX</c>。</param>
    /// <param name="borderless">无边框窗口 → 启动参数。</param>
    /// <param name="dx12">DX12 模式；<b>只有 ZZZ</b> 有对应的 env（原神的 DX12 不走 DXVK）。</param>
    /// <param name="fsr">FSR。</param>
    public static GameAdapterEnvResult BuildEnv(
        string gameKind,
        bool wineWayland,
        bool timeoutFix,
        bool borderless,
        bool dx12,
        bool fsr)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        var extraArguments = new List<string>();

        if (wineWayland)
        {
            // 存在且为空：不是删除（D7）
            env["DISPLAY"] = WaylandDisplayValue;
        }

        if (timeoutFix)
        {
            env["WINE_ENABLE_TIMEOUT_FIX"] = "1";
        }

        if (dx12 && IsZzz(gameKind))
        {
            // dxvk-nvapi 在 DX12 模式下需要显式开启
            // https://github.com/jp7677/dxvk-nvapi#wine--wine-staging
            env["DXVK_ENABLE_NVAPI"] = "1";
            env["VKD3D_CONFIG"] = "dxr";
        }

        if (fsr)
        {
            env["WINE_FULLSCREEN_FSR"] = "1";
            env["WINE_FULLSCREEN_FSR_STRENGTH"] = DefaultFsrStrength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (borderless)
        {
            extraArguments.AddRange(BorderlessArguments);
        }

        return new GameAdapterEnvResult
        {
            Env = env,
            ExtraArguments = extraArguments,
        };
    }


    /// <summary>
    /// 构建层 8 的 env（按 <see cref="GameDefinition.Kind"/> 判断游戏特化）。
    /// </summary>
    public static GameAdapterEnvResult BuildEnv(
        GameDefinition definition,
        bool wineWayland,
        bool timeoutFix,
        bool borderless,
        bool dx12,
        bool fsr)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return BuildEnv(definition.Kind, wineWayland, timeoutFix, borderless, dx12, fsr);
    }


    /// <summary>
    /// 只构建 env（不含启动参数）。
    /// </summary>
    public static Dictionary<string, string> BuildEnvOnly(
        string gameKind,
        bool wineWayland,
        bool timeoutFix,
        bool dx12,
        bool fsr)
        => BuildEnv(gameKind, wineWayland, timeoutFix, borderless: false, dx12, fsr).Env;


    /// <summary>
    /// 完整的游戏特化启动参数（borderless + DX12 + FSR 窗口模式）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="BuildEnv"/> 的 <see cref="GameAdapterEnvResult.ExtraArguments"/> 分开：
    /// 后者只含 borderless，便于调用方只取"窗口"相关参数。
    /// </remarks>
    public static List<string> BuildArguments(
        string gameKind,
        bool borderless,
        bool dx12,
        bool fsr)
    {
        var arguments = new List<string>();
        if (dx12 && IsZzz(gameKind))
        {
            arguments.AddRange(Dx12Arguments);
        }

        if (borderless)
        {
            arguments.AddRange(BorderlessArguments);
        }

        if (fsr)
        {
            arguments.AddRange(FsrExclusiveWindowArguments);
        }

        return arguments;
    }


    /// <summary>该游戏是否支持 DX12 特化（本项目里只有 ZZZ）。</summary>
    public static bool SupportsDx12(string? gameKind) => IsZzz(gameKind);


    /// <summary>
    /// 游戏是否有 B 站渠道（仅原神）。见 <c>docs/04-FEATURES.md</c> §2.7。
    /// </summary>
    public static bool SupportsBilibiliChannel(string? gameKind)
        => gameKind is not null && gameKind.Equals(GameCatalog.Genshin, StringComparison.OrdinalIgnoreCase);


    /// <summary>
    /// 把层 8 应用到一份已有 env（返回新字典，不改入参）。
    /// </summary>
    public static Dictionary<string, string> MergeInto(
        IReadOnlyDictionary<string, string> inherited,
        GameAdapterEnvResult result)
    {
        ArgumentNullException.ThrowIfNull(inherited);
        ArgumentNullException.ThrowIfNull(result);

        var env = new Dictionary<string, string>(inherited, StringComparer.Ordinal);
        foreach (var (key, value) in result.Env)
        {
            env[key] = value;
        }

        return env;
    }


    private static bool IsZzz(string? gameKind)
        => gameKind is not null && gameKind.Equals(GameCatalog.Zzz, StringComparison.OrdinalIgnoreCase);
}
