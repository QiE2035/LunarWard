using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using LunarWard.Frameworks.WebView.Interop;

namespace LunarWard.Frameworks.WebView;

/// <summary>
/// GTK3 的专属线程与主循环。
///
/// Avalonia 已经是应用的主循环，GTK 不能抢主线程，所以这里起一个后台线程：线程里
/// <c>gtk_init_check</c> → 建控件 → <c>gtk_main()</c>，之后所有 GTK 调用都通过
/// <see cref="Post"/>/<see cref="Invoke"/> 投递到这个线程执行（GLib 的
/// <c>g_idle_add</c> 是线程安全的，可以在任意线程挂 source）。
///
/// 后端选择：X11 下用 <c>gtk_plug_new(xid)</c> 把 WebView 嵌进 Avalonia 的窗口；
/// Wayland 下没有 XID，GDK 也没有可嵌入的子窗口句柄，控件会降级成独立 GTK 窗口。
/// GTK 的 GDK 后端在第一次 gtk_init 时确定，之后无法切换，所以这里在初始化前按需求
/// 设置 <c>GDK_BACKEND</c>。
/// </summary>
internal static class GtkRuntime
{
    private const string BackendEnvVar = "LUNARWARD_WEBVIEW_GDK_BACKEND";

    private static readonly object StateGate = new();
    private static readonly ConcurrentQueue<Action> Pending = new();
    private static readonly GSourceFunc IdleCallback = DrainPending;

    private static Thread? _thread;
    private static ManualResetEventSlim? _ready;
    private static volatile bool _initialized;
    private static volatile int _gtkThreadId;
    private static volatile string _backend = "unknown";
    private static volatile string _initError = string.Empty;
    private static int _idleScheduled;

    public static bool IsInitialized => _initialized && _thread is not null;

    /// <summary>GDK 后端：x11 / wayland / broadway / 其它 gdk_display_get_name 的前缀。</summary>
    public static string Backend => _backend;

    public static bool IsGtkThread => _gtkThreadId != 0 && Environment.CurrentManagedThreadId == _gtkThreadId;

    /// <summary>
    /// 启动 GTK 线程。<paramref name="preferX11"/> 为 true 时会强制
    /// <c>GDK_BACKEND=x11</c>（嵌入 X11 XID 的前提）；否则沿用会话默认后端
    /// （Wayland 会话里就是 Wayland，走独立窗口降级路径）。
    /// </summary>
    public static bool EnsureStarted(bool preferX11, out string error)
    {
        lock (StateGate)
        {
            if (_thread is not null)
            {
                error = _initialized ? string.Empty : _initError;
                return _initialized;
            }

            if (!WebViewAvailability.IsAvailable)
            {
                error = WebViewAvailability.MissingLibrariesMessage;
                _initError = error;
                WebViewLog.Error($"WebKitGTK 不可用，跳过 GTK 初始化。{error}");
                return false;
            }

            string? forced = Environment.GetEnvironmentVariable(BackendEnvVar);
            if (!string.IsNullOrWhiteSpace(forced))
            {
                Libc.Set("GDK_BACKEND", forced);
                WebViewLog.Info($"GDK_BACKEND 由 {BackendEnvVar} 指定为 '{forced}'");
            }
            else if (preferX11)
            {
                // 只有 XID 嵌入需要 X11 后端；Wayland 会话下 GTK 走 XWayland 也能连上。
                Libc.Set("GDK_BACKEND", "x11");
                WebViewLog.Info("需要 X11 XID 嵌入，设置 GDK_BACKEND=x11（GTK 的 GDK 后端在 gtk_init 时确定，之后不可切换）");
            }

            _ready = new ManualResetEventSlim(false);
            _thread = new Thread(GtkThreadMain)
            {
                IsBackground = true,
                Name = "LunarWard-GTK",
            };
            _thread.Start();

            // gtk_init 里有 D-Bus / 字体缓存等 IO，给足 30 秒。
            if (!_ready.Wait(TimeSpan.FromSeconds(30)))
            {
                error = "GTK 初始化超时（30s）";
                _initError = error;
                WebViewLog.Error(error);
                return false;
            }

            error = _initialized ? string.Empty : _initError;
            return _initialized;
        }
    }

    private static void GtkThreadMain()
    {
        try
        {
            _gtkThreadId = Environment.CurrentManagedThreadId;

            // argv[0] 必须是有效字符串：NULL 会让 GLib 打印
            // "g_path_get_basename: assertion 'file_name != NULL' failed"。
            IntPtr argv0 = Marshal.StringToCoTaskMemUTF8("lunarward");
            IntPtr argv = Marshal.AllocHGlobal(IntPtr.Size * 2);
            Marshal.WriteIntPtr(argv, 0, argv0);
            Marshal.WriteIntPtr(argv, IntPtr.Size, IntPtr.Zero);
            int argc = 1;
            IntPtr argvPtr = argv;

            bool ok = false;
            try
            {
                ok = Gtk.gtk_init_check(ref argc, ref argvPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
                Marshal.FreeCoTaskMem(argv0);
            }

            if (!ok)
            {
                _initError = "gtk_init_check 失败：没有可用的显示（DISPLAY / WAYLAND_DISPLAY 都不可用，或 X11 连接被拒绝）";
                WebViewLog.Error(_initError);
                _initialized = false;
                _ready?.Set();
                return;
            }

            _backend = DescribeBackend();
            _initialized = true;
            WebViewLog.Info($"GTK {Gtk.gtk_get_major_version()}.{Gtk.gtk_get_minor_version()}.{Gtk.gtk_get_micro_version()} 初始化完成"
                            + $"，GDK 后端 = {_backend}，线程 = {Environment.CurrentManagedThreadId}");
            _ready?.Set();

            Gtk.gtk_main();
            WebViewLog.Info("GTK 主循环已退出");
        }
        catch (Exception ex)
        {
            _initError = $"GTK 线程异常：{ex.GetType().Name}: {ex.Message}";
            WebViewLog.Error("GTK 线程异常", ex);
            _ready?.Set();
        }
    }

    private static string DescribeBackend()
    {
        try
        {
            IntPtr display = Gdk.gdk_display_get_default();
            if (display == IntPtr.Zero)
            {
                return "none";
            }

            IntPtr namePtr = Gdk.gdk_display_get_name(display);
            string name = namePtr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(namePtr) ?? string.Empty;
            string backend = Environment.GetEnvironmentVariable("GDK_BACKEND") ?? "unknown";
            WebViewLog.Info($"GDK display name = '{name}'（GDK_BACKEND 请求值 = '{backend}'，原生 environ 实测 = '{Libc.GetNative("GDK_BACKEND") ?? "<null>"}'）");

            if (name.StartsWith("wayland", StringComparison.OrdinalIgnoreCase))
            {
                return "wayland";
            }

            if (name.StartsWith("broadway", StringComparison.OrdinalIgnoreCase))
            {
                return "broadway";
            }

            if (name.StartsWith(":"))
            {
                return "x11";
            }

            return string.IsNullOrEmpty(name) ? "none" : name;
        }
        catch (Exception ex)
        {
            WebViewLog.Warn($"读取 GDK 后端失败：{ex.Message}");
            return "unknown";
        }
    }

    /// <summary>把一段工作投递到 GTK 线程（不等待）。</summary>
    public static void Post(Action action)
    {
        if (action is null)
        {
            return;
        }

        if (!IsInitialized)
        {
            WebViewLog.Warn("GTK 尚未初始化，丢弃一次投递");
            return;
        }

        Pending.Enqueue(action);
        if (Interlocked.Exchange(ref _idleScheduled, 1) == 0)
        {
            Glib.g_idle_add(IdleCallback, IntPtr.Zero);
        }
    }

    /// <summary>把一段工作投递到 GTK 线程并等待完成。</summary>
    public static void Invoke(Action action) => InvokeAsync(action).GetAwaiter().GetResult();

    public static T Invoke<T>(Func<T> func) => InvokeAsync(func).GetAwaiter().GetResult();

    /// <summary>在线程上执行并返回 Task；GTK 线程上调用则直接执行（避免自锁）。</summary>
    public static Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (IsGtkThread)
        {
            try
            {
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!IsInitialized)
        {
            tcs.SetException(new InvalidOperationException("GTK 未初始化"));
            return tcs.Task;
        }

        Pending.Enqueue(() =>
        {
            try
            {
                tcs.TrySetResult(func());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (Interlocked.Exchange(ref _idleScheduled, 1) == 0)
        {
            Glib.g_idle_add(IdleCallback, IntPtr.Zero);
        }

        return tcs.Task;
    }

    public static Task InvokeAsync(Action action) => InvokeAsync(() =>
    {
        action();
        return true;
    });

    /// <summary>同步版本带超时，避免 UI 线程在原生库卡死时永久阻塞。</summary>
    public static T InvokeWithTimeout<T>(Func<T> func, TimeSpan? timeout = null)
    {
        var task = InvokeAsync(func);
        TimeSpan wait = timeout ?? TimeSpan.FromSeconds(30);
        if (!task.Wait(wait))
        {
            throw new TimeoutException($"GTK 调用超时（{wait.TotalSeconds:0}s）");
        }

        return task.GetAwaiter().GetResult();
    }

    public static void Shutdown()
    {
        if (!IsInitialized)
        {
            return;
        }

        WebViewLog.Info("请求 GTK 主循环退出");
        Post(Gtk.gtk_main_quit);
    }

    private static int DrainPending(IntPtr userData)
    {
        // 先把标志清零：drain 期间新投递会再挂一个 idle source，不会丢任务。
        Interlocked.Exchange(ref _idleScheduled, 0);

        while (Pending.TryDequeue(out Action? action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                WebViewLog.Error("GTK 线程执行任务异常", ex);
            }
        }

        return Glib.GSourceRemove;
    }
}
