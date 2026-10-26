using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using LunarWard.Features.Screenshot;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;


namespace LunarWard.Features.Overlay;

/// <summary>
/// 上游 <c>Starward.Features.Overlay.OverlayWindow</c>（148 行）的 Linux 移植。
/// 覆盖在游戏窗口客户区上的半透明遮罩：Escape 或窗口失活即淡出隐藏。
/// </summary>
/// <remarks>
/// 平台替换（逐处已用 <c>// 平台替换：</c> 标注，并登记 docs/09-LINUX-DIVERGENCES.md 第二十六节）：
/// WinUI <c>Window</c>/<c>AppWindow</c> → <see cref="WindowEx"/>；<c>Microsoft.UI.Windowing.OverlappedPresenter</c>
/// → Avalonia <c>Window</c> 属性；<c>User32</c> 客户区几何/前台激活 → <c>xdotool</c>；
/// <c>SystemBackdrop</c>/合成器动画 → <see cref="TransparentBackdrop"/> + <c>Transitions</c>。
/// </remarks>
public sealed partial class OverlayWindow : WindowEx
{


    // 平台替换：上游有 `private static bool SystemWin11;`（Win11 才给覆盖层加底部圆角）。
    // Linux 没有「圆角窗口 + 阴影边框」的等价语义，判定恒为 false，因此删除该字段，
    // ShowActive 里固定用直角（相当于上游非 Win11 分支）。


    public OverlayWindow()
    {
        this.InitializeComponent();
        // 平台替换：上游 `SystemBackdrop = new TransparentBackdrop();`（WinUI 系统背景 + DWM 模糊）。
        // Avalonia 由 Helpers/TransparentBackdrop 把窗口切成透明背景，不支持透明合成时退化为不透明黑底并写日志。
        new TransparentBackdrop().ApplyTo(this);
        InitializeWindow();
        // 平台替换：上游在 `Activated` 事件里判断 `WindowActivationState.Deactivated` 后隐藏；
        // Avalonia 有独立的 `Deactivated` 事件，语义等价。
        Deactivated += OverlayWindow_Deactivated;
    }





    private void InitializeWindow()
    {
        // 平台替换：上游 `AppWindow.TitleBar.ExtendsContentIntoTitleBar = true` +
        // `OverlappedPresenter`（不可最大化 / 不可缩放 / 去边框标题栏 / 置顶）+ User32 去 WS_DLGFRAME。
        // Avalonia 对应的是 Window 自身的属性，语义逐条对应。
        ExtendClientAreaToDecorationsHint = true;
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowInTaskbar = false;
    }



    private void OverlayWindow_Deactivated(object? sender, EventArgs e)
    {
        // 平台替换：上游 `WindowActivatedEventArgs` → Avalonia `EventArgs`（Deactivated 事件）。
        this.Hide();
    }


    // 平台替换：上游 `KeyRoutedEventArgs`（WinUI）→ Avalonia `KeyEventArgs`；
    // `Windows.System.VirtualKey.Escape` → `Avalonia.Input.Key.Escape`。
    private void RootGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            this.Hide();
        }
    }



    private CancellationTokenSource? _windowCancellationTokenSource;



    public void ShowActive(RunningGame runningGame)
    {
        try
        {
            _windowCancellationTokenSource?.Cancel();
            // 平台替换：上游 `var hwnd = runningGame.WindowHandle;`。.NET/Linux 上 `Process.MainWindowHandle` 恒 0，
            // 因此句柄由抓屏后端（xdotool search --pid）解析，见 ScreenCaptureHelper.FindWindowByPid。
            nint hwnd = runningGame.WindowHandle != 0 ? runningGame.WindowHandle : ScreenCaptureHelper.FindWindowByPid(runningGame.Pid);
            // 平台替换：上游 `User32.GetClientRect(hwnd)` + `User32.ClientToScreen(hwnd, ref point)` +
            // `AppWindow.MoveAndResize(new RectInt32(...))`。Linux 用 `xdotool getwindowgeometry --shell` 拿客户区几何，
            // 再换算成 Avalonia 的物理像素 Position/ClientSize。
            (int x, int y, int width, int height) = GetClientBounds(hwnd);
            Position = new PixelPoint(x, y);
            ClientSize = new Size(width, height);
            // 平台替换：上游 Win11 下比较客户区/窗口高度决定底部圆角（`RootGrid.CornerRadius = new CornerRadius(0, 0, 8, 8)`）；
            // Linux 游戏窗口没有圆角合成，覆盖层恒直角，避免把游戏画面四角裁掉。
            RootGrid.CornerRadius = new CornerRadius(0);
            // 平台替换：上游 `User32.SetForegroundWindow(hwnd)`（把游戏窗口拉回前台）。Linux 用 xdotool 尽力而为。
            TryActivateGameWindow(hwnd);
            base.Show();
            RootGrid.Opacity = 1;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }



    // 平台替换：上游 `public new async void Hide()`。Avalonia 的 `WindowEx.Hide` 是 virtual，用 `new` 遮蔽，
    // 效果相同：先把 RootGrid 淡出（Transitions 0.3s），延迟 300ms 后真正隐藏窗口。
    public new async void Hide()
    {
        try
        {
            _windowCancellationTokenSource ??= new CancellationTokenSource();
            CancellationToken token = _windowCancellationTokenSource.Token;
            RootGrid.Opacity = 0;
            await Task.Delay(300);
            if (token.IsCancellationRequested)
            {
                return;
            }
            base.Hide();
        }
        catch { }
        finally
        {
            _windowCancellationTokenSource = null;
        }
    }


    // 平台替换：上游 `public new async void Close()`（WinUI Window 没有可遮蔽的 async Close）；
    // Avalonia 里同样用 `new` 遮蔽 `Window.Close()`，先淡出再关闭。
    public new async void Close()
    {
        try
        {
            RootGrid.Opacity = 0;
            await Task.Delay(300);
            base.Hide();
            base.Close();
        }
        catch { }
    }



    /// <summary>
    /// 平台替换：对应上游 `User32.GetClientRect` + `User32.ClientToScreen`。
    /// X11 用 <c>xdotool getwindowgeometry --shell</c> 查询窗口几何；Wayland 下拿不到时退回主屏工作区，
    /// 保证覆盖层至少可见（不静默黑屏）。
    /// </summary>
    private (int X, int Y, int Width, int Height) GetClientBounds(nint hwnd)
    {
        if (hwnd != 0 && TryGetWindowGeometry(hwnd, out int x, out int y, out int width, out int height))
        {
            return (x, y, width, height);
        }
        var screen = Screens.ScreenFromPoint(new PixelPoint(0, 0)) ?? Screens.Primary;
        if (screen is not null)
        {
            PixelRect area = screen.WorkingArea;
            return (area.X, area.Y, area.Width, area.Height);
        }
        return (0, 0, 1280, 720);
    }


    private static bool TryGetWindowGeometry(nint hwnd, out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        if (!ScreenCaptureHelper.IsToolAvailable("xdotool"))
        {
            return false;
        }
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "xdotool",
                ArgumentList = { "getwindowgeometry", "--shell", hwnd.ToString(CultureInfo.InvariantCulture) },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            if (process.ExitCode != 0)
            {
                return false;
            }
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int index = line.IndexOf('=');
                if (index < 0)
                {
                    continue;
                }
                string key = line[..index];
                string value = line[(index + 1)..];
                if (!int.TryParse(value, CultureInfo.InvariantCulture, out int number))
                {
                    continue;
                }
                switch (key)
                {
                    case "X": x = number; break;
                    case "Y": y = number; break;
                    case "WIDTH": width = number; break;
                    case "HEIGHT": height = number; break;
                }
            }
            return width > 0 && height > 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }


    /// <summary>
    /// 平台替换：对应上游 `User32.SetForegroundWindow`。X11 用 <c>xdotool windowactivate</c> 尽力激活游戏窗口；
    /// Wayland 下 xdotool 通常不可用，失败静默（覆盖层依旧显示在游戏上方）。
    /// </summary>
    private static void TryActivateGameWindow(nint hwnd)
    {
        if (hwnd == 0 || !ScreenCaptureHelper.IsToolAvailable("xdotool"))
        {
            return;
        }
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "xdotool",
                ArgumentList = { "windowactivate", hwnd.ToString(CultureInfo.InvariantCulture) },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            process?.WaitForExit(2000);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }



}
