namespace LunarWard.Core;

/// <summary>
/// 按 GameBiz 查询游戏安装路径。
/// </summary>
/// <remarks>
/// <para>
/// 上游 <c>Starward.Core</c> 在 <c>Gacha/GachaLogClient.cs</c> 中直接调用
/// <c>Registry.GetValue</c> 读取启动器注册表项。Linux 没有注册表，因此把这唯一一处
/// Windows 依赖抽成本接口（见 <c>docs/02-ARCHITECTURE.md</c> §3.2）。
/// </para>
/// <para>
/// 实现约定：
/// </para>
/// <list type="bullet">
///   <item>Windows 实现读 <c>HKEY_CURRENT_USER\Software\miHoYo\...</c> 的 <c>InstallPath</c></item>
///   <item>Linux 实现读 Wine prefix 的 <c>user.reg</c>，或直接读游戏目录的 <c>config.ini</c></item>
///   <item>兜底实现（<see cref="NullGameInstallPathProvider"/>）永远返回 <c>null</c></item>
/// </list>
/// </remarks>
public interface IGameInstallPathProvider
{
    /// <summary>
    /// 按 GameBiz 查询游戏安装路径；返回 <c>null</c> 表示未知。
    /// </summary>
    /// <param name="gameBiz">GameBiz 字符串，见 <see cref="GameBiz"/>。</param>
    string? GetInstallPath(GameBiz gameBiz);


    /// <summary>
    /// 按注册表键路径查询"启动器安装路径"。
    /// </summary>
    /// <param name="registryKey">
    /// 注册表键的完整路径，例如 <c>HKEY_CURRENT_USER\Software\miHoYo\原神</c>
    /// （见 <c>GameRegistry.GamePath_*</c> 常量）。
    /// </param>
    /// <remarks>
    /// 上游的语义是"启动器自己的安装目录，里面放着一个写了 <c>game_install_path=</c>
    /// 的 <c>config.ini</c>"。Linux 实现可以返回游戏目录本身。
    /// </remarks>
    string? GetLauncherPath(string registryKey);
}


/// <summary>
/// 兜底实现：永远返回 <c>null</c>。用于无前缀信息或测试场景。
/// </summary>
public sealed class NullGameInstallPathProvider : IGameInstallPathProvider
{
    public static NullGameInstallPathProvider Instance { get; } = new();


    public string? GetInstallPath(GameBiz gameBiz) => null;


    public string? GetLauncherPath(string registryKey) => null;
}
