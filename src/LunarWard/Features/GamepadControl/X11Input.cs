using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LunarWard.Features.GamepadControl;

/// <summary>
/// X11/XWayland 的全局输入注入：替代上游 <c>H.InputSimulator</c>（Win32 <c>SendInput</c>）。
/// </summary>
/// <remarks>
/// 选用 XTest 扩展（<c>libXtst.so.6</c> + <c>libX11.so.6</c>）的理由：
/// <list type="bullet">
/// <item>它是 X11 协议层的“假输入”标准通道，XWayland 下的窗口（绝大多数 Linux 游戏）也接收得到，
/// 效果与上游 <c>SendInput</c> 最接近：直接产生键码/按钮事件，而不是往某个窗口塞消息。</item>
/// <item>原生 Wayland 会话没有等价的全局注入协议（<c>libei</c> 需要合成器授权、各桌面实现不一致），
/// 此时 <see cref="Opened"/> 会是 true（因为存在 XWayland DISPLAY）但事件只对 XWayland 窗口有效——
/// 这一点由 <see cref="GamepadController"/> 记录在 <c>docs/09-LINUX-DIVERGENCES.md</c>，不假装成功。</item>
/// </list>
/// 所有 Xlib 调用串行化在 <see cref="_sync"/> 里（Xlib 的 Display 不是线程安全的）。
/// </remarks>
internal sealed class X11Input
{

    private readonly object _sync = new();

    private readonly IntPtr _display;

    private readonly Dictionary<int, uint> _keycodeCache = new();


    public X11Input()
    {
        try
        {
            if (!X11Native.TryLoad(out string? error))
            {
                ErrorMessage = error;
                return;
            }
            _display = X11Native.OpenDisplay(null);
            if (_display == IntPtr.Zero)
            {
                ErrorMessage = "XOpenDisplay 失败：没有可用的 X11 DISPLAY（原生 Wayland 会话下无法全局注入）";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }


    public bool Opened => _display != IntPtr.Zero;


    public string? ErrorMessage { get; }



    public void KeyDown(VirtualKey virtualKey) => KeyEvent((VirtualKeyCode)(ushort)virtualKey, true);


    public void KeyUp(VirtualKey virtualKey) => KeyEvent((VirtualKeyCode)(ushort)virtualKey, false);


    public void KeyDown(VirtualKeyCode virtualKeyCode) => KeyEvent(virtualKeyCode, true);


    public void KeyUp(VirtualKeyCode virtualKeyCode) => KeyEvent(virtualKeyCode, false);



    /// <summary>
    /// 上游 <c>InputSimulator.Keyboard.ModifiedKeyStroke(modifiers, keys)</c> 的等价实现：
    /// 依次按下修饰键 → 按下/松开每个键 → 逆序松开修饰键。
    /// </summary>
    public void ModifiedKeyStroke(IReadOnlyList<VirtualKeyCode> modifiers, IReadOnlyList<VirtualKeyCode> keys)
    {
        if (!Opened)
        {
            return;
        }
        lock (_sync)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                KeyEventCore(modifiers[i], true);
            }
            for (int i = 0; i < keys.Count; i++)
            {
                KeyEventCore(keys[i], true);
                KeyEventCore(keys[i], false);
            }
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                KeyEventCore(modifiers[i], false);
            }
            X11Native.Flush(_display);
        }
    }



    private void KeyEvent(VirtualKeyCode virtualKeyCode, bool isPress)
    {
        if (!Opened)
        {
            return;
        }
        lock (_sync)
        {
            KeyEventCore(virtualKeyCode, isPress);
            X11Native.Flush(_display);
        }
    }


    private void KeyEventCore(VirtualKeyCode virtualKeyCode, bool isPress)
    {
        uint keycode = GetKeycode(virtualKeyCode);
        if (keycode != 0)
        {
            X11Native.TestFakeKeyEvent(_display, keycode, isPress ? 1 : 0, 0);
        }
    }



    private uint GetKeycode(VirtualKeyCode virtualKeyCode)
    {
        int key = (int)virtualKeyCode;
        if (_keycodeCache.TryGetValue(key, out uint cached))
        {
            return cached;
        }
        string? keysymName = KeysymName(virtualKeyCode);
        if (keysymName is null)
        {
            _keycodeCache[key] = 0;
            return 0;
        }
        UIntPtr keysym = X11Native.StringToKeysym(keysymName);
        if (keysym == UIntPtr.Zero)
        {
            _keycodeCache[key] = 0;
            return 0;
        }
        byte keycode = X11Native.KeysymToKeycode(_display, keysym);
        _keycodeCache[key] = keycode;
        return keycode;
    }



    /// <summary>
    /// Win32 虚拟键码 → X11 keysym 名（<c>XStringToKeysym</c> 的输入）。
    /// </summary>
    internal static string? KeysymName(VirtualKeyCode vk)
    {
        return (int)vk switch
        {
            0x08 => "BackSpace",
            0x09 => "Tab",
            0x0D => "Return",
            0x10 => "Shift_L",
            0x11 => "Control_L",
            0x12 => "Alt_L",
            0x13 => "Pause",
            0x14 => "Caps_Lock",
            0x1B => "Escape",
            0x20 => "space",
            0x21 => "Prior",
            0x22 => "Next",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2C => "Print",
            0x2D => "Insert",
            0x2E => "Delete",
            >= 0x30 and <= 0x39 => ((char)('0' + ((int)vk - 0x30))).ToString(),
            >= 0x41 and <= 0x5A => ((char)('a' + ((int)vk - 0x41))).ToString(),
            0x5B => "Super_L",
            0x5C => "Super_R",
            >= 0x60 and <= 0x69 => "KP_" + ((char)('0' + ((int)vk - 0x60))),
            0x6A => "KP_Multiply",
            0x6B => "KP_Add",
            0x6D => "KP_Subtract",
            0x6E => "KP_Decimal",
            0x6F => "KP_Divide",
            >= 0x70 and <= 0x7B => "F" + (((int)vk - 0x70) + 1),
            0x90 => "Num_Lock",
            0x91 => "Scroll_Lock",
            0xBF => "slash",
            0xC0 => "grave",
            0xBA => "semicolon",
            0xBB => "equal",
            0xBC => "comma",
            0xBD => "minus",
            0xBE => "period",
            0xDB => "bracketleft",
            0xDC => "backslash",
            0xDD => "bracketright",
            0xDE => "apostrophe",
            _ => null,
        };
    }



    public void MoveMouseBy(int dx, int dy)
    {
        if (!Opened)
        {
            return;
        }
        lock (_sync)
        {
            X11Native.TestFakeRelativeMotionEvent(_display, dx, dy, 0);
            X11Native.Flush(_display);
        }
    }



    /// <summary>
    /// 鼠标滚轮：X11 用按钮 4/5（垂直）与 6/7（水平）的按下+松开表示一格。
    /// </summary>
    public void Scroll(int dx, int dy)
    {
        if (!Opened)
        {
            return;
        }
        lock (_sync)
        {
            uint horizontalButton = dx >= 0 ? 7u : 6u;
            uint verticalButton = dy <= 0 ? 5u : 4u;
            int horizontalCount = Math.Min(Math.Abs(dx), 10);
            int verticalCount = Math.Min(Math.Abs(dy), 10);
            for (int i = 0; i < horizontalCount; i++)
            {
                X11Native.TestFakeButtonEvent(_display, horizontalButton, 1, 0);
                X11Native.TestFakeButtonEvent(_display, horizontalButton, 0, 0);
            }
            for (int i = 0; i < verticalCount; i++)
            {
                X11Native.TestFakeButtonEvent(_display, verticalButton, 1, 0);
                X11Native.TestFakeButtonEvent(_display, verticalButton, 0, 0);
            }
            X11Native.Flush(_display);
        }
    }


    public void MouseLeftButtonDown() => MouseButton(1, true);

    public void MouseLeftButtonUp() => MouseButton(1, false);

    public void MouseRightButtonDown() => MouseButton(3, true);

    public void MouseRightButtonUp() => MouseButton(3, false);


    private void MouseButton(uint button, bool isPress)
    {
        if (!Opened)
        {
            return;
        }
        lock (_sync)
        {
            X11Native.TestFakeButtonEvent(_display, button, isPress ? 1 : 0, 0);
            X11Native.Flush(_display);
        }
    }



    /// <summary>
    /// 取全局鼠标位置（对应上游 <c>User32.GetCursorPos</c>）。Wayland 原生会话或 X11 不可用时返回 false。
    /// </summary>
    public bool TryGetCursorPosition(out int x, out int y)
    {
        x = 0;
        y = 0;
        if (!Opened)
        {
            return false;
        }
        lock (_sync)
        {
            return X11Native.QueryPointer(_display, X11Native.DefaultRootWindow(_display), out _, out _, out x, out y, out _, out _, out _) != 0;
        }
    }



    #region 原生函数表（NativeLibrary 探测，缺失不抛异常）


    /// <summary>
    /// Xlib + XTest 的函数表：运行时 <see cref="NativeLibrary.TryLoad(string, out IntPtr)"/> 绑定，
    /// 任一库缺失都只是让 <see cref="TryLoad"/> 返回 false 并给出可读原因（不抛 DllNotFoundException）。
    /// </summary>
    private static unsafe class X11Native
    {

        private static bool _loaded;

        private static bool _available;

        private static string? _error;

        private static IntPtr _x11;

        private static IntPtr _xtst;

        private static delegate* unmanaged[Cdecl]<string?, IntPtr> _openDisplay;

        private static delegate* unmanaged[Cdecl]<string, UIntPtr> _stringToKeysym;

        private static delegate* unmanaged[Cdecl]<IntPtr, UIntPtr, byte> _keysymToKeycode;

        private static delegate* unmanaged[Cdecl]<IntPtr, int> _flush;

        private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr> _defaultRootWindow;

        private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, out IntPtr, out IntPtr, out int, out int, out int, out int, out uint, int> _queryPointer;

        private static delegate* unmanaged[Cdecl]<IntPtr, uint, int, ulong, int> _testFakeKeyEvent;

        private static delegate* unmanaged[Cdecl]<IntPtr, uint, int, ulong, int> _testFakeButtonEvent;

        private static delegate* unmanaged[Cdecl]<IntPtr, int, int, ulong, int> _testFakeRelativeMotionEvent;


        public static bool TryLoad(out string? error)
        {
            if (_loaded)
            {
                error = _error;
                return _available;
            }
            _loaded = true;
            if (!NativeLibrary.TryLoad("libX11.so.6", out _x11))
            {
                _error = "未找到 libX11.so.6（无法使用 XTest 注入）";
                error = _error;
                return false;
            }
            if (!NativeLibrary.TryLoad("libXtst.so.6", out _xtst))
            {
                _error = "未找到 libXtst.so.6（XTest 扩展不可用，请安装 libxtst6）";
                error = _error;
                return false;
            }
            try
            {
                _openDisplay = (delegate* unmanaged[Cdecl]<string?, IntPtr>)NativeLibrary.GetExport(_x11, "XOpenDisplay");
                _stringToKeysym = (delegate* unmanaged[Cdecl]<string, UIntPtr>)NativeLibrary.GetExport(_x11, "XStringToKeysym");
                _keysymToKeycode = (delegate* unmanaged[Cdecl]<IntPtr, UIntPtr, byte>)NativeLibrary.GetExport(_x11, "XKeysymToKeycode");
                _flush = (delegate* unmanaged[Cdecl]<IntPtr, int>)NativeLibrary.GetExport(_x11, "XFlush");
                _defaultRootWindow = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)NativeLibrary.GetExport(_x11, "XDefaultRootWindow");
                _queryPointer = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, out IntPtr, out IntPtr, out int, out int, out int, out int, out uint, int>)NativeLibrary.GetExport(_x11, "XQueryPointer");
                _testFakeKeyEvent = (delegate* unmanaged[Cdecl]<IntPtr, uint, int, ulong, int>)NativeLibrary.GetExport(_xtst, "XTestFakeKeyEvent");
                _testFakeButtonEvent = (delegate* unmanaged[Cdecl]<IntPtr, uint, int, ulong, int>)NativeLibrary.GetExport(_xtst, "XTestFakeButtonEvent");
                _testFakeRelativeMotionEvent = (delegate* unmanaged[Cdecl]<IntPtr, int, int, ulong, int>)NativeLibrary.GetExport(_xtst, "XTestFakeRelativeMotionEvent");
            }
            catch (EntryPointNotFoundException ex)
            {
                _error = $"X11/XTest 动态库缺少必要符号（{ex.Message}）";
                error = _error;
                return false;
            }
            _available = true;
            error = null;
            return true;
        }


        public static IntPtr OpenDisplay(string? name) => _openDisplay(name);

        public static UIntPtr StringToKeysym(string name) => _stringToKeysym(name);

        public static byte KeysymToKeycode(IntPtr display, UIntPtr keysym) => _keysymToKeycode(display, keysym);

        public static int Flush(IntPtr display) => _flush(display);

        public static IntPtr DefaultRootWindow(IntPtr display) => _defaultRootWindow(display);

        public static int QueryPointer(IntPtr display, IntPtr window, out IntPtr rootReturn, out IntPtr childReturn,
                                       out int rootX, out int rootY, out int winX, out int winY, out uint maskReturn)
            => _queryPointer(display, window, out rootReturn, out childReturn, out rootX, out rootY, out winX, out winY, out maskReturn);

        public static int TestFakeKeyEvent(IntPtr display, uint keycode, int isPress, ulong delay)
            => _testFakeKeyEvent(display, keycode, isPress, delay);

        public static int TestFakeButtonEvent(IntPtr display, uint button, int isPress, ulong delay)
            => _testFakeButtonEvent(display, button, isPress, delay);

        public static int TestFakeRelativeMotionEvent(IntPtr display, int x, int y, ulong delay)
            => _testFakeRelativeMotionEvent(display, x, y, delay);

    }


    #endregion


}
