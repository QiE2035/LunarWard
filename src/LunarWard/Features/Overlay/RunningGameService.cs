using Avalonia.Threading;
using LunarWard.Core;
using LunarWard.Features.GamepadControl;
using LunarWard.Features.Screenshot;
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
/// <item><c>OverlayWindow</c> 已移植（同目录 <c>OverlayWindow.axaml(.cs)</c>）。上游 <see cref="OpenOverlayWindow"/>
/// 第一行就是 <c>return false;</c>（其后半段是死代码）；Linux 端按后半段的意图接线，去掉 early return，
/// 能否显示由「窗口句柄是否可解析」决定（docs/09 §8.5、第二十六节）。</item>
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
                // 平台替换：上游 `_overlayWindow?.DispatcherQueue.TryEnqueue(() => { if (_overlayWindow?.AppWindow is not null) _overlayWindow.Close(); });`
                // Avalonia 对应 Dispatcher.UIThread.Post；OverlayWindow 在 UI 线程创建/关闭。
                Dispatcher.UIThread.Post(() =>
                {
                    _overlayWindow?.Close();
                    _overlayWindow = null;
                });
                _timer.Stop();
                GamepadController.RestoreGamepadGuideButtonForGameBarBecauseOfGameExit();
            }
        }
    }



    private static OverlayWindow? _overlayWindow;


    public static bool OpenOverlayWindow()
    {
        // 平台替换：上游第一行是 `return false;`（Overlay 未启用，其后为死代码），这里按后半段意图实现。
        // 上游的 `User32.IsIconic(hwnd)`（最小化判定）与前台钩子在 Linux 无等价物：
        // 改为「能否用 xdotool 解析到游戏窗口句柄」，解析不到就不显示覆盖层。
        RunningGame? runningGame = _latestActiveGame;
        if (runningGame is null)
        {
            return false;
        }
        nint hwnd = runningGame.WindowHandle != 0 ? runningGame.WindowHandle : ScreenCaptureHelper.FindWindowByPid(runningGame.Pid);
        if (hwnd == 0)
        {
            return false;
        }
        if (runningGame.WindowHandle == 0)
        {
            runningGame.WindowHandle = hwnd;
        }
        // 平台替换：上游在 UI 线程直接 `new OverlayWindow()` / `ShowActive`；调用点可能在游戏进程的事件线程，
        // 这里统一切到 Avalonia UI 线程。
        Dispatcher.UIThread.Post(() =>
        {
            _overlayWindow ??= new OverlayWindow();
            _overlayWindow.ShowActive(runningGame);
        });
        return true;
    }


}
