using System;

namespace LunarWard.App.Services;

/// <summary>主窗口关闭行为（对应上游 <c>AppConfig.CloseWindowOption</c> / <c>MainWindowCloseOption</c>）。</summary>
public enum CloseWindowAction
{
    /// <summary>最小化到系统托盘（上游 <c>Hide</c>）。</summary>
    Hide,

    /// <summary>完全退出（上游 <c>Exit</c>）。</summary>
    Exit,

    /// <summary>每次询问（上游 <c>0</c> / 未设置：弹 <c>MainWindowCloseDialog</c>）。</summary>
    Ask,
}


/// <summary>
/// 主窗口关闭流程的纯判定：配置字符串 → 动作，再结合托盘是否可用得出实际执行的动作。
/// </summary>
/// <remarks>
/// <para>
/// 拆成纯函数是为了可单测（测试进程没有窗口/托盘），也为了让「托盘不可用时不要静默隐藏」
/// 这条差异只写在一个地方：
/// </para>
/// <list type="bullet">
///   <item><description><c>Hide</c> + 托盘可用 → 藏到托盘；</description></item>
///   <item><description><c>Hide</c> + 托盘不可用 → 退化为真正退出（否则窗口就消失了，用户再也叫不回来）；</description></item>
///   <item><description><c>Exit</c> → 退出；</description></item>
///   <item><description><c>Ask</c>/空/未知值 → 弹询问对话框（托盘不可用时对话框只提供「完全退出」）。</description></item>
/// </list>
/// </remarks>
public static class CloseWindowDecision
{
    /// <summary>配置值：最小化到托盘。</summary>
    public const string HideOption = "Hide";

    /// <summary>配置值：完全退出。</summary>
    public const string ExitOption = "Exit";

    /// <summary>配置值：每次询问。</summary>
    public const string AskOption = "Ask";


    /// <summary>把配置字符串解析成 <see cref="CloseWindowAction"/>；无法识别时按「每次询问」处理。</summary>
    public static CloseWindowAction Parse(string? option)
        => option?.Trim().ToLowerInvariant() switch
        {
            "hide" => CloseWindowAction.Hide,
            "exit" => CloseWindowAction.Exit,
            _ => CloseWindowAction.Ask,
        };


    /// <summary>
    /// 关闭窗口时实际要做的动作：托盘不可用时 <c>Hide</c> 退化为 <c>Exit</c>。
    /// </summary>
    public static CloseWindowAction Resolve(string? option, bool trayAvailable)
        => Parse(option) switch
        {
            CloseWindowAction.Hide when trayAvailable => CloseWindowAction.Hide,
            CloseWindowAction.Hide => CloseWindowAction.Exit,
            CloseWindowAction.Exit => CloseWindowAction.Exit,
            _ => CloseWindowAction.Ask,
        };


    /// <summary>询问对话框里「最小化到托盘」这一项是否可选（没有托盘就没得选）。</summary>
    public static bool CanOfferHide(bool trayAvailable) => trayAvailable;
}
