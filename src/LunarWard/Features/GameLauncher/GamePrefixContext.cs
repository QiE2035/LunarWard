namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 「当前游戏注册表」的 Linux 对应物：当前档案（profile）的 Wine prefix 路径。
/// </summary>
/// <remarks>
/// <para>
/// 上游 Starward 的所有「读游戏设置 / 读账号 / 读 UID」都是直接访问
/// <c>HKEY_CURRENT_USER\Software\miHoYo\...</c>——Windows 下一个用户只有一份注册表，
/// 所以上游的服务不需要知道"当前是谁"。Linux 下每个档案一个 prefix，
/// 于是这份「当前 prefix」成了平台层面的显式上下文。
/// </para>
/// <para>
/// 约定：外壳（<c>MainWindow</c>/页面 <c>OnNavigatedTo</c>）在切换档案时赋值一次；
/// 各 Feature 服务从这里取 prefix，再交给 <c>WineAccountService</c> 读写 <c>user.reg</c>。
/// 这是<b>唯一</b>新增的跨文件状态，见 <c>docs/09-LINUX-DIVERGENCES.md</c>。
/// </para>
/// </remarks>
public static class GamePrefixContext
{

    /// <summary>当前档案的 Wine prefix 目录；<c>null</c> 表示尚未选择档案。</summary>
    public static string? CurrentPrefix { get; set; }


    /// <summary>
    /// 当前档案使用的 wine 可执行文件（可能是 proton 内置 wine / 自建 wine）；
    /// <c>null</c> 时回退到 PATH 里的 <c>wine</c>。
    /// </summary>
    public static string? CurrentWineBinary { get; set; }

}
