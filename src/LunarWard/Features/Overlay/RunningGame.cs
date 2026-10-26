using LunarWard.Core;
using System.Diagnostics;

namespace LunarWard.Features.Overlay;

public class RunningGame
{

    public GameBiz GameBiz { get; set; }

    public Process Process { get; set; }

    public int Pid { get; set; }

    public string Name { get; set; }

    public nint WindowHandle
    {
        get
        {
            // 上游：`User32.IsWindow(field) && User32.IsWindowVisible(field)` 失效时用 `Process.MainWindowHandle` 刷新。
            // Linux 没有等价的可视性查询：`Process.MainWindowHandle` 在 .NET/Linux 上恒为 0，
            // 真正的窗口定位交给截图/Overlay 后端（X11 xdotool search --pid / xwininfo，docs/09 §8.5）。
            if (field == 0)
            {
                Process.Refresh();
                field = Process.MainWindowHandle;
            }
            return field;
        }
        set { field = value; }
    }


    /// <summary>
    /// 上游是 <c>User32.HWINEVENTHOOK</c>（前台窗口事件钩子）。Linux 无等价物，
    /// 这里只保留字段承载，事件来源由后续 Overlay 后端接管（docs/09 §8.5）。
    /// </summary>
    public nint WinEventHook { get; set; }



    public RunningGame(GameBiz gameBiz, Process process)
    {
        GameBiz = gameBiz;
        Process = process;
        Pid = process.Id;
        Name = process.ProcessName;
        WindowHandle = process.MainWindowHandle;
    }




}
