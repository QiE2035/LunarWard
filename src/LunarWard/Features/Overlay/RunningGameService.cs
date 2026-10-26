using LunarWard.Core;
using LunarWard.Features.GamepadControl;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Timers;

namespace LunarWard.Features.Overlay;

/// <summary>
/// 上游 <c>Starward.Features.Overlay.RunningGameService</c>（138 行）的 Linux 移植（最小可用：进程绑定 + 状态查询）。
/// </summary>
/// <remarks>
/// 与上游的差异（均已登记 docs/09 §8.5）：
/// <list type="bullet">
/// <item>Win32 前台窗口钩子（<c>User32.SetWinEventHook</c>/<c>UnhookWinEvent</c>/<c>WinEventProc</c>）整体移除：
/// Linux 无等价物，「最近活跃游戏」退化为「最后加入的游戏」。</item>
/// <item><c>OverlayWindow</c> 尚未移植，<see cref="OpenOverlayWindow"/> 保持上游「恒返回 false」的行为。</item>
/// <item><c>Process.MainWindowHandle</c> 在 .NET/Linux 上恒为 0，窗口句柄留给抓屏后端提供。</item>
/// </list>
/// </remarks>
internal static class RunningGameService
{

    private static readonly Lock _runningGameLock = new();


    private static readonly List<RunningGame> _runningGames = new();


    private static readonly System.Timers.Timer _timer = new();


    private static RunningGame? _latestActiveGame;


    static RunningGameService()
    {
        _timer.Elapsed += CheckGameExit;
    }



    public static void AddRuninngGame(GameBiz gameBiz, Process process)
    {
        try
        {
            lock (_runningGameLock)
            {
                if (_runningGames.FirstOrDefault(x => x.Pid == process.Id) is not RunningGame runningGame)
                {
                    runningGame = new RunningGame(gameBiz, process);
                    // 上游在此处 User32.SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …)；
                    // Linux 无前台窗口事件钩子，改为「后加入的游戏视为最近活跃」，X11 焦点后端落地后再接。
                    _runningGames.Add(runningGame);
                    _latestActiveGame = runningGame;
                    Debug.WriteLine($"Added running game: {runningGame.Name} ({runningGame.Pid})");
                }
                GamepadController.DisableGamepadGuideButtonForGameBarBecauseOfGameStart();
                _timer.Start();
            }
        }
        catch { }
    }



    public static RunningGame? GetLatestActiveGame()
    {
        return _latestActiveGame;
    }


    public static int GetRunningGameCount()
    {
        return _runningGames.Count;
    }


    private static void CheckGameExit(object? sender, ElapsedEventArgs e)
    {
        lock (_runningGameLock)
        {
            for (int i = 0; i < _runningGames.Count; i++)
            {
                RunningGame runningGame = _runningGames[i];
                if (runningGame.Process.HasExited)
                {
                    // 上游此处 UnhookWinEvent(runningGame.WinEventHook)（Windows 专有），Linux 无需释放。
                    _runningGames.RemoveAt(i);
                    if (_latestActiveGame?.Pid == runningGame.Pid)
                    {
                        _latestActiveGame = null;
                    }
                    i--; // Adjust index after removal
                    Debug.WriteLine($"Running game exited: {runningGame.Name} ({runningGame.Pid})");
                }
            }
            if (_runningGames.Count == 0)
            {
                // 上游此处关闭 OverlayWindow（`_overlayWindow?.DispatcherQueue.TryEnqueue(…Close)`）；
                // OverlayWindow 尚未移植（docs/09 §8.4），落地后在这里接回。
                _timer.Stop();
                GamepadController.RestoreGamepadGuideButtonForGameBarBecauseOfGameExit();
            }
        }
    }



    public static bool OpenOverlayWindow()
    {
        // 上游该方法函数体第一行就是 `return false;`（Overlay 尚未启用），照抄该行为；
        // OverlayWindow 移植后按其后半段恢复：判断 _latestActiveGame/WindowHandle，再 ShowActive。
        return false;
    }


}
