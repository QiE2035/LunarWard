using LunarWard.Features.Screenshot;
using LunarWard.Frameworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Timer = System.Timers.Timer;

namespace LunarWard.Features.GamepadControl;

/// <summary>
/// 手柄增强：读取手柄输入、把摇杆/按键映射成鼠标键盘操作、处理 Xbox Guide/Share 键。
/// <b>移植自</b> <c>Starward/src/Starward/Features/GamepadControl/GamepadController.cs</c>。
/// </summary>
/// <remarks>
/// 平台方案（逐条登记于 <c>docs/09-LINUX-DIVERGENCES.md</c> 的「九、手柄增强」）：
/// <list type="bullet">
/// <item><b>输入读取</b>：上游用 <c>SharpGameInput.V0.IGameInput</c>（Windows GameInput）。Linux 改用 <b>SDL2</b>
/// （<c>P/Invoke libSDL2-2.0.so.0</c>，本机 2.32.0 已存在）：SDL 的 <c>SDL_GameController</c> 提供与
/// <c>Windows.Gaming.Input.Gamepad</c> 同级的抽象——标准化按键/轴映射（内含覆盖绝大多数手柄的 mapping DB）、
/// 热插拔事件（<c>SDL_CONTROLLERDEVICEADDED/REMOVED</c>）、Guide 键（<c>SDL_CONTROLLER_BUTTON_GUIDE</c>）与
/// Share 键（Xbox 系列手柄映射到 <c>MISC1</c>）。
/// 选择 SDL2 而不是直接读 evdev：evdev 只给出内核原始事件，跨设备没有映射数据库（需要 udev hwdb + 逐设备适配），
/// 而 SDL2 的 mapping DB 与 Proton/游戏用的是同一套，行为最接近上游的 GameInput 抽象。
/// </list>
/// <item><b>输入注入</b>：上游用 <c>H.InputSimulator</c>（Win32 SendInput）。Linux 改用 <b>XTest 扩展</b>
/// （<c>libXtst.so.6</c> + <c>libX11.so.6</c>，本机均存在）：X11 会话与 XWayland（<c>DISPLAY=:0</c>）下可全局注入键盘/鼠标事件。
/// 原生 Wayland 会话没有等价的全局注入协议（<c>libei</c> 需要合成器显式授权且各桌面实现不一），
/// 因此 <see cref="InputInjectionAvailable"/> 为 false，此时只读手柄、不伪造输入，并在
/// <see cref="UnavailableReason"/> 里给出可读原因供设置页展示（不静默、不崩溃）。
/// </item>
/// <item><b>Xbox Game Bar</b>：上游改注册表（<c>HKCU\Software\Microsoft\GameBar</c>、
/// <c>...\GameDVR</c>）开关 Game Bar 与录屏。Linux 无对应物，这些方法保留签名、只写一次 Warning 日志（不空实现地假装成功）。
/// </item>
/// <item><b>GameInputRedist</b>：上游检测/引导安装 <c>GameInputRedist.dll</c>。Linux 无此组件，
/// 改用 <see cref="SdlAvailable"/>/<see cref="InputInjectionAvailable"/>/<see cref="UnavailableReason"/> 三件套表达同样的降级状态。
/// </item>
/// <item><b>时间单位</b>：上游 GameInput 时间戳单位是 100µs（<c>100_000</c> = 10ms、<c>400_000</c> = 400ms），
/// 本移植改用 <see cref="Stopwatch"/> 毫秒（10 / 400），阈值语义一致。
/// </item>
/// </list>
/// </remarks>
internal static class GamepadController
{

    private static SdlGameController? _controller;

    private static X11Input? _x11;

    private static Timer? _inputLoopTimer;

    private static CancellationTokenSource? _guideButtonCancellationTokenSource;

    private static SimulateInputTipsWindow? _simulateInputTipsWindow;

    private static readonly Stopwatch _clock = Stopwatch.StartNew();


    /// <summary>
    /// 初始化手柄控制。上游是 <c>Initialize(DispatcherQueue?)</c>；Avalonia 没有可选 DispatcherQueue，
    /// 保留同签名的 <see cref="Dispatcher"/> 参数以便逐行对照（忽略传入值，统一用 <see cref="Dispatcher.UIThread"/>）。
    /// </summary>
    public static void Initialize(Dispatcher? dispatcher = null)
    {
        try
        {
            _controller = new SdlGameController();
            SdlAvailable = _controller.Initialized;
            if (!SdlAvailable)
            {
                UnavailableReason = _controller.ErrorMessage ?? "SDL2 初始化失败";
                Serilog.Log.Warning("GamepadController: SDL2 不可用，手柄增强已禁用：{Reason}", UnavailableReason);
                return;
            }
            _x11 = new X11Input();
            InputInjectionAvailable = _x11.Opened;
            if (!InputInjectionAvailable)
            {
                UnavailableReason = _x11.ErrorMessage ?? "当前会话不支持 XTest 注入（原生 Wayland）";
                Serilog.Log.Warning("GamepadController: 输入注入不可用，仅读取手柄：{Reason}", UnavailableReason);
            }
            _inputLoopTimer = new Timer(16);
            _inputLoopTimer.Elapsed += _inputLoopTimer_Elapsed;
            GamepadConnected = _controller.HasController;
            EnableGamepadSimulateInput = AppConfig.EnableGamepadSimulateInput;
            GamepadGuideButtonMode = AppConfig.GamepadGuideButtonMode;
            GamepadShareButtonMode = AppConfig.GamepadShareButtonMode;
            SetGamepadGuideButtonMapKeys(AppConfig.GamepadGuideButtonMapKeys, out _);
            SetGamepadShareButtonMapKeys(AppConfig.GamepadShareButtonMapKeys, out _);
            Initialized = true;
            DeviceOrEnableChanged();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Initialize GamepadController failed");
            UnavailableReason = ex.Message;
        }
    }



    public static bool Initialized { get; private set; }


    /// <summary>
    /// SDL2 是否可用（对应上游 <c>GameInputRedistInstalled</c> 的位置：输入后端是否就绪）。
    /// </summary>
    public static bool SdlAvailable { get; private set; }


    /// <summary>
    /// 是否能读到手柄（= SDL2 后端就绪）。缺库时为 false，原因见 <see cref="UnavailableReason"/>。
    /// </summary>
    public static bool InputReadingAvailable => SdlAvailable;


    /// <summary>
    /// 是否能把输入注入到本会话（X11/XWayland + XTest）。原生 Wayland 下为 false。
    /// </summary>
    public static bool InputInjectionAvailable { get; private set; }


    /// <summary>
    /// 全局输入注入器（XTest）。供 <see cref="SimulateInputTipsWindow"/> 取光标位置等用。
    /// </summary>
    internal static X11Input? Input => _x11;


    /// <summary>
    /// 降级原因（供设置页展示，<see cref="InputInjectionAvailable"/> 或 <see cref="SdlAvailable"/> 为 false 时非空）。
    /// </summary>
    public static string? UnavailableReason { get; private set; }


    public static bool EnableGamepadSimulateInput { get; set { field = value; DeviceOrEnableChanged(); } }


    public static bool GamepadConnected { get; set { field = value; DeviceOrEnableChanged(); } }


    public static int GamepadGuideButtonMode
    {
        get; set
        {
            field = value;
            if (value is 1)
            {
                if (RunningGameCount() == 0)
                {
                    RestoreGamepadGuideButtonForGameBarBecauseOfGameExit();
                }
            }
            else if (value is 2)
            {
                DisableGamepadGuideButtonForGameBar();
                if (RunningGameCount() > 0)
                {
                    DisableGamepadGuideButtonForGameBarBecauseOfGameStart();
                }
            }
            else
            {
                RestoreGamepadGuideButtonForGameBar();
            }
        }
    }


    public static int GamepadShareButtonMode
    {
        get; set
        {
            field = value;
            if (value is 1)
            {
                EnableXboxGameBarCapture();
            }
            else
            {
                DisbaleXboxGameBarCapture();
            }
        }
    }


    public static (VirtualKeyCode[] Modifiers, VirtualKeyCode[] Keys) GamepadGuideButtonMapKeys
    {
        get; set
        {
            field = value;
            (VirtualKeyCode[] modifiers, VirtualKeyCode[] keys) = value;
            GuideButtonModifiers = (VirtualKeyCode[])modifiers?.Clone()! ?? [];
            GuideButtonKeys = (VirtualKeyCode[])keys?.Clone()! ?? [];
        }
    }


    public static (VirtualKeyCode[] Modifiers, VirtualKeyCode[] Keys) GamepadShareButtonMapKeys
    {
        get; set
        {
            field = value;
            (VirtualKeyCode[] modifiers, VirtualKeyCode[] keys) = value;
            ShareButtonModifiers = (VirtualKeyCode[])modifiers?.Clone()! ?? [];
            ShareButtonKeys = (VirtualKeyCode[])keys?.Clone()! ?? [];
        }
    }


    private static VirtualKeyCode[] GuideButtonModifiers = [];

    private static VirtualKeyCode[] GuideButtonKeys = [];

    private static VirtualKeyCode[] ShareButtonModifiers = [];

    private static VirtualKeyCode[] ShareButtonKeys = [];



    private static int RunningGameCount()
    {
        // 上游：RunningGameService.GetRunningGameCount()。RunningGameService 属于 Features/Overlay（另一移植任务），
        // 且 Xbox Game Bar 相关方法在 Linux 本来就没有效果，因此这里不引用它（docs/09）。
        return 0;
    }



    private static void DeviceOrEnableChanged()
    {
        try
        {
            if (!Initialized)
            {
                return;
            }
            if (GamepadConnected && EnableGamepadSimulateInput)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }
        catch { }
    }


    private static void Start()
    {
        _inputLoopTimer?.Start();
    }


    private static void Stop()
    {
        _inputLoopTimer?.Stop();
        _simulateInputEnabled = false;
        HideSimulateInputTipsWindow();
    }



    #region Key Maps


    public static bool GetKeyMaps(string? value, out string keysTextOrErrorKey, [NotNullWhen(true)] out List<VirtualKeyCode>? modifiers, [NotNullWhen(true)] out List<VirtualKeyCode>? keysList)
    {
        modifiers = null;
        keysList = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            keysTextOrErrorKey = string.Empty;
            return false;
        }
        modifiers = new();
        keysList = new();
        string[] strings = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string key in strings)
        {
            if (KeyNameToVirtualKey.TryGetValue(key.ToUpper(), out VirtualKey vk))
            {
                if (vk is VirtualKey.Shift or VirtualKey.Control or VirtualKey.Menu or VirtualKey.LeftWindows)
                {
                    modifiers.Add((VirtualKeyCode)vk);
                }
                else
                {
                    keysList.Add((VirtualKeyCode)vk);
                }
            }
            else
            {
                keysTextOrErrorKey = key;
                modifiers = null;
                keysList = null;
                return false;
            }
        }
        keysTextOrErrorKey = string.Join(" ", modifiers.Concat(keysList).Select(x => VirtualKeyToKeyName.GetValueOrDefault((VirtualKey)x)));
        return true;
    }




    public static bool SetGamepadGuideButtonMapKeys(string? value, [NotNullWhen(true)] out string? keysTextOrErrorKey)
    {
        if (GetKeyMaps(value, out keysTextOrErrorKey, out var modifiers, out var keys))
        {
            GamepadGuideButtonMapKeys = (modifiers.ToArray(), keys.ToArray());
            AppConfig.GamepadGuideButtonMapKeys = value;
            return true;
        }
        return false;
    }



    public static bool SetGamepadShareButtonMapKeys(string? value, [NotNullWhen(true)] out string? keysTextOrErrorKey)
    {
        if (GetKeyMaps(value, out keysTextOrErrorKey, out var modifiers, out var keys))
        {
            GamepadShareButtonMapKeys = (modifiers.ToArray(), keys.ToArray());
            AppConfig.GamepadShareButtonMapKeys = value;
            return true;
        }
        return false;
    }




    public static string GetGamepadGuideButtonMapKeysText()
    {
        if (!Initialized)
        {
            return string.Empty;
        }
        return string.Join(" ", GamepadGuideButtonMapKeys.Modifiers.Concat(GamepadGuideButtonMapKeys.Keys)
                                                             .Select(x => VirtualKeyToKeyName.GetValueOrDefault((VirtualKey)x)));
    }



    public static string GetGamepadShareButtonMapKeysText()
    {
        if (!Initialized)
        {
            return string.Empty;
        }
        return string.Join(" ", GamepadShareButtonMapKeys.Modifiers.Concat(GamepadShareButtonMapKeys.Keys)
                                                            .Select(x => VirtualKeyToKeyName.GetValueOrDefault((VirtualKey)x)));
    }



    public static readonly Dictionary<string, VirtualKey> KeyNameToVirtualKey = new()
    {
        ["SHIFT"] = VirtualKey.Shift,
        ["CTRL"] = VirtualKey.Control,
        ["CONTROL"] = VirtualKey.Control,
        ["ALT"] = VirtualKey.Menu,
        ["MENU"] = VirtualKey.Menu,
        ["WIN"] = VirtualKey.LeftWindows,
        ["WINDOW"] = VirtualKey.LeftWindows,
        ["WINDOWS"] = VirtualKey.LeftWindows,

        ["INS"] = VirtualKey.Insert,
        ["INSERT"] = VirtualKey.Insert,
        ["DEL"] = VirtualKey.Delete,
        ["DELETE"] = VirtualKey.Delete,
        ["HOME"] = VirtualKey.Home,
        ["END"] = VirtualKey.End,
        ["PAGEUP"] = VirtualKey.PageUp,
        ["PGUP"] = VirtualKey.PageUp,
        ["PAGEDOWN"] = VirtualKey.PageDown,
        ["PGDN"] = VirtualKey.PageDown,
        ["←"] = VirtualKey.Left,
        ["↑"] = VirtualKey.Up,
        ["→"] = VirtualKey.Right,
        ["↓"] = VirtualKey.Down,
        ["LEFT"] = VirtualKey.Left,
        ["UP"] = VirtualKey.Up,
        ["RIGHT"] = VirtualKey.Right,
        ["DWON"] = VirtualKey.Down,

        ["0"] = VirtualKey.Number0,
        ["1"] = VirtualKey.Number1,
        ["2"] = VirtualKey.Number2,
        ["3"] = VirtualKey.Number3,
        ["4"] = VirtualKey.Number4,
        ["5"] = VirtualKey.Number5,
        ["6"] = VirtualKey.Number6,
        ["7"] = VirtualKey.Number7,
        ["8"] = VirtualKey.Number8,
        ["9"] = VirtualKey.Number9,

        ["A"] = VirtualKey.A, ["B"] = VirtualKey.B, ["C"] = VirtualKey.C, ["D"] = VirtualKey.D,
        ["E"] = VirtualKey.E, ["F"] = VirtualKey.F, ["G"] = VirtualKey.G, ["H"] = VirtualKey.H,
        ["I"] = VirtualKey.I, ["J"] = VirtualKey.J, ["K"] = VirtualKey.K, ["L"] = VirtualKey.L,
        ["M"] = VirtualKey.M, ["N"] = VirtualKey.N, ["O"] = VirtualKey.O, ["P"] = VirtualKey.P,
        ["Q"] = VirtualKey.Q, ["R"] = VirtualKey.R, ["S"] = VirtualKey.S, ["T"] = VirtualKey.T,
        ["U"] = VirtualKey.U, ["V"] = VirtualKey.V, ["W"] = VirtualKey.W, ["X"] = VirtualKey.X,
        ["Y"] = VirtualKey.Y, ["Z"] = VirtualKey.Z,

        ["PAD0"] = VirtualKey.NumberPad0,
        ["PAD1"] = VirtualKey.NumberPad1,
        ["PAD2"] = VirtualKey.NumberPad2,
        ["PAD3"] = VirtualKey.NumberPad3,
        ["PAD4"] = VirtualKey.NumberPad4,
        ["PAD5"] = VirtualKey.NumberPad5,
        ["PAD6"] = VirtualKey.NumberPad6,
        ["PAD7"] = VirtualKey.NumberPad7,
        ["PAD8"] = VirtualKey.NumberPad8,
        ["PAD9"] = VirtualKey.NumberPad9,

        ["PAD+"] = VirtualKey.Add,
        ["PAD-"] = VirtualKey.Subtract,
        ["PAD*"] = VirtualKey.Multiply,
        ["PAD/"] = VirtualKey.Divide,
        ["PAD."] = VirtualKey.Decimal,

        ["F1"] = VirtualKey.F1, ["F2"] = VirtualKey.F2, ["F3"] = VirtualKey.F3, ["F4"] = VirtualKey.F4,
        ["F5"] = VirtualKey.F5, ["F6"] = VirtualKey.F6, ["F7"] = VirtualKey.F7, ["F8"] = VirtualKey.F8,
        ["F9"] = VirtualKey.F9, ["F10"] = VirtualKey.F10, ["F11"] = VirtualKey.F11, ["F12"] = VirtualKey.F12,

        ["`"] = (VirtualKey)192,
        ["-"] = (VirtualKey)189,
        ["="] = (VirtualKey)187,
        ["["] = (VirtualKey)219,
        ["]"] = (VirtualKey)221,
        ["\\"] = (VirtualKey)220,
        [";"] = (VirtualKey)186,
        ["'"] = (VirtualKey)222,
        [","] = (VirtualKey)188,
        ["."] = (VirtualKey)190,
        ["/"] = (VirtualKey)191,

        ["ESC"] = VirtualKey.Escape,
        ["ESCAPE"] = VirtualKey.Escape,
        ["SPACE"] = VirtualKey.Space,
        ["TAB"] = VirtualKey.Tab,
        ["ENTER"] = VirtualKey.Enter,
        ["BACKSPACE"] = VirtualKey.Back,
        ["BACK"] = VirtualKey.Back,
        ["CAPSLOCK"] = VirtualKey.CapitalLock,
        ["CAPITALLOCK"] = VirtualKey.CapitalLock,
        ["NUMLOCK"] = VirtualKey.NumberKeyLock,
        ["NUMLK"] = VirtualKey.NumberKeyLock,
        ["NUMBERKEYLOCK"] = VirtualKey.NumberKeyLock,
        ["SCROLL"] = VirtualKey.Scroll,
        ["SCROLLLOCK"] = VirtualKey.Scroll,
        ["PAUSE"] = VirtualKey.Pause,
        ["PRINT"] = VirtualKey.Print,
        ["PRTSC"] = VirtualKey.Print,
        ["PRINTSCREEN"] = VirtualKey.Print,
    };


    public static readonly Dictionary<VirtualKey, string> VirtualKeyToKeyName = new()
    {
        [VirtualKey.Shift] = "Shift",
        [VirtualKey.Control] = "Ctrl",
        [VirtualKey.Menu] = "Alt",
        [VirtualKey.LeftWindows] = "Win",
        [VirtualKey.RightWindows] = "Win",

        [VirtualKey.Insert] = "Insert",
        [VirtualKey.Delete] = "Delete",
        [VirtualKey.Home] = "Home",
        [VirtualKey.End] = "End",
        [VirtualKey.PageUp] = "PageUp",
        [VirtualKey.PageDown] = "PageDown",
        [VirtualKey.Left] = "←",
        [VirtualKey.Up] = "↑",
        [VirtualKey.Right] = "→",
        [VirtualKey.Down] = "↓",

        [VirtualKey.Number0] = "0", [VirtualKey.Number1] = "1", [VirtualKey.Number2] = "2",
        [VirtualKey.Number3] = "3", [VirtualKey.Number4] = "4", [VirtualKey.Number5] = "5",
        [VirtualKey.Number6] = "6", [VirtualKey.Number7] = "7", [VirtualKey.Number8] = "8",
        [VirtualKey.Number9] = "9",

        [VirtualKey.A] = "A", [VirtualKey.B] = "B", [VirtualKey.C] = "C", [VirtualKey.D] = "D",
        [VirtualKey.E] = "E", [VirtualKey.F] = "F", [VirtualKey.G] = "G", [VirtualKey.H] = "H",
        [VirtualKey.I] = "I", [VirtualKey.J] = "J", [VirtualKey.K] = "K", [VirtualKey.L] = "L",
        [VirtualKey.M] = "M", [VirtualKey.N] = "N", [VirtualKey.O] = "O", [VirtualKey.P] = "P",
        [VirtualKey.Q] = "Q", [VirtualKey.R] = "R", [VirtualKey.S] = "S", [VirtualKey.T] = "T",
        [VirtualKey.U] = "U", [VirtualKey.V] = "V", [VirtualKey.W] = "W", [VirtualKey.X] = "X",
        [VirtualKey.Y] = "Y", [VirtualKey.Z] = "Z",

        [VirtualKey.NumberPad0] = "Pad0", [VirtualKey.NumberPad1] = "Pad1",
        [VirtualKey.NumberPad2] = "Pad2", [VirtualKey.NumberPad3] = "Pad3",
        [VirtualKey.NumberPad4] = "Pad4", [VirtualKey.NumberPad5] = "Pad5",
        [VirtualKey.NumberPad6] = "Pad6", [VirtualKey.NumberPad7] = "Pad7",
        [VirtualKey.NumberPad8] = "Pad8", [VirtualKey.NumberPad9] = "Pad9",

        [VirtualKey.Add] = "Pad+",
        [VirtualKey.Subtract] = "Pad-",
        [VirtualKey.Multiply] = "Pad*",
        [VirtualKey.Divide] = "Pad/",
        [VirtualKey.Decimal] = "Pad.",

        [VirtualKey.F1] = "F1", [VirtualKey.F2] = "F2", [VirtualKey.F3] = "F3", [VirtualKey.F4] = "F4",
        [VirtualKey.F5] = "F5", [VirtualKey.F6] = "F6", [VirtualKey.F7] = "F7", [VirtualKey.F8] = "F8",
        [VirtualKey.F9] = "F9", [VirtualKey.F10] = "F10", [VirtualKey.F11] = "F11", [VirtualKey.F12] = "F12",

        [(VirtualKey)192] = "`",
        [(VirtualKey)189] = "-",
        [(VirtualKey)187] = "=",
        [(VirtualKey)219] = "[",
        [(VirtualKey)221] = "]",
        [(VirtualKey)220] = "\\",
        [(VirtualKey)186] = ";",
        [(VirtualKey)222] = "'",
        [(VirtualKey)188] = ",",
        [(VirtualKey)190] = ".",
        [(VirtualKey)191] = "/",

        [VirtualKey.Escape] = "Esc",
        [VirtualKey.Space] = "Space",
        [VirtualKey.Tab] = "Tab",
        [VirtualKey.Enter] = "Enter",
        [VirtualKey.Back] = "Backspace",
        [VirtualKey.CapitalLock] = "CapsLock",
        [VirtualKey.NumberKeyLock] = "NumberLock",
        [VirtualKey.Scroll] = "ScrollLock",
        [VirtualKey.Pause] = "Pause",
        [VirtualKey.Print] = "PrintScreen",
    };


    #endregion



    #region Device Callback


    private static int gamepadCount = 0;


    /// <summary>
    /// 对应上游 <c>GameInputDeviceCallback</c>：SDK 的热插拔事件在 <see cref="SdlGameController"/> 里被翻译成这个回调。
    /// </summary>
    private static void GameInputDeviceCallback(bool connected, bool firstConnected)
    {
        if (connected)
        {
            GamepadConnected = true;
            if (firstConnected)
            {
                gamepadCount++;
            }
        }
        if (!connected)
        {
            gamepadCount--;
            if (gamepadCount <= 0)
            {
                gamepadCount = 0;
                GamepadConnected = false;
            }
        }
    }


    #endregion



    #region System Button Callback


    private static bool guideLongPressTriggered;


    private static async void OnGuideDown()
    {
        if (EnableGamepadSimulateInput)
        {
            _guideButtonCancellationTokenSource?.Cancel();
            _guideButtonCancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _guideButtonCancellationTokenSource.Token;
            await Task.Delay(600);
            if (token.IsCancellationRequested)
            {
                return;
            }
            guideLongPressTriggered = true;
            GuideLongPress();
        }
        else
        {
            GuideClick();
        }
    }


    private static void OnGuideUp()
    {
        if (EnableGamepadSimulateInput)
        {
            _guideButtonCancellationTokenSource?.Cancel();
            if (!guideLongPressTriggered)
            {
                GuideClick();
            }
            guideLongPressTriggered = false;
        }
    }


    private static void GuideClick()
    {
        switch (GamepadGuideButtonMode)
        {
            case 1:
                Dispatcher.UIThread.Post(() =>
                {
                    ScreenCaptureService.Capture();
                });
                break;
            case 2:
                if (GuideButtonModifiers.Length > 0 || GuideButtonKeys.Length > 0)
                {
                    _x11?.ModifiedKeyStroke(GuideButtonModifiers, GuideButtonKeys);
                }
                break;
            default:
                break;
        }
    }


    private static void GuideLongPress()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // 上游：MainWindow.Current.ShowByGamepad()（Windows 上把主窗口从后台唤起）。
                if (WindowEx.MainWindow is { } window)
                {
                    window.Show();
                    window.Activate();
                }
                if (_simulateInputTipsWindow is null)
                {
                    _simulateInputTipsWindow = new SimulateInputTipsWindow();
                }
                _simulateInputTipsWindow.Activate();
                _simulateInputEnabled = true;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "GamepadController: 显示模拟输入提示窗口失败");
            }
        });
    }


    private static void OnShareDown()
    {
        switch (GamepadShareButtonMode)
        {
            case 1:
                Dispatcher.UIThread.Post(() =>
                {
                    ScreenCaptureService.Capture();
                });
                break;
            case 2:
                if (ShareButtonModifiers.Length > 0 || ShareButtonKeys.Length > 0)
                {
                    _x11?.ModifiedKeyStroke(ShareButtonModifiers, ShareButtonKeys);
                }
                break;
            default:
                break;
        }
    }


    #endregion



    #region Input Loop


    private static long _lastScrollTimestamp;

    private static long _lastButtonsTimestamp;

    private static GamepadButtons _lastGamepadButtons;

    private static bool _simulateInputEnabled;

    private static int mouseMoveRepeatCount;

    private static long _lastSwitchWindowTimestamp;

    private static GamepadButtons _lastShoulderButtons;

    private static GamepadState _latestState;

    private static bool _latestStateValid;


    private static void _inputLoopTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            InputLoop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    private static void InputLoop()
    {
        if (_controller is null || !_controller.Initialized)
        {
            return;
        }
        _controller.PumpEvents(GameInputDeviceCallback, OnGuideDown, OnGuideUp, OnShareDown);
        if (!_controller.GetState(out GamepadState state))
        {
            return;
        }
        _latestState = state;
        _latestStateValid = true;
        CheckGamepadThumbSticks(state.Buttons);
        if (!_simulateInputEnabled)
        {
            return;
        }
        if (!InputInjectionAvailable)
        {
            return;
        }
        long currentTimestamp = _clock.ElapsedMilliseconds;
        MouseMove(state);
        SwitchWindow(state, currentTimestamp);
        if (currentTimestamp - _lastScrollTimestamp > 10)
        {
            _lastScrollTimestamp = currentTimestamp;
            MouseScroll(state);
        }
        if (currentTimestamp - _lastButtonsTimestamp > 0)
        {
            _lastButtonsTimestamp = currentTimestamp;
            ButtonClick(state);
        }
    }


    private static void CheckGamepadThumbSticks(GamepadButtons buttons)
    {
        GamepadButtons thumbSticks = GamepadButtons.LeftThumbstick | GamepadButtons.RightThumbstick;
        GamepadButtons current = buttons & thumbSticks;
        if (current == thumbSticks && current != (_lastGamepadButtons & thumbSticks))
        {
            _simulateInputEnabled = !_simulateInputEnabled;
            if (_simulateInputEnabled)
            {
                ShowSimulateInputTipsWindow();
            }
            else
            {
                HideSimulateInputTipsWindow();
            }
        }
        _lastGamepadButtons = buttons;
    }


    private static void MouseMove(GamepadState state)
    {
        const float deadZone = 0.1f;
        const float speedFactor = 10f;
        const float exponent = 1.5f;
        float x = state.LeftThumbstickX;
        float y = state.LeftThumbstickY;
        float magnitude = MathF.Sqrt(x * x + y * y);
        mouseMoveRepeatCount++;
        if (magnitude >= deadZone)
        {
            float exp = MathF.Pow(magnitude, exponent);
            float gain = 1f + Math.Min(mouseMoveRepeatCount, 250) / 125f;
            int dx = (int)(x * speedFactor * exp * gain);
            int dy = (int)(-y * speedFactor * exp * gain);
            if (dx != 0 || dy != 0)
            {
                _x11?.MoveMouseBy(dx, dy);
            }
        }
        else
        {
            mouseMoveRepeatCount = 0;
        }
    }


    private static void MouseScroll(GamepadState state)
    {
        float x = state.RightThumbstickX;
        float y = state.RightThumbstickY;
        float magnitude = MathF.Sqrt(x * x + y * y);
        if (magnitude < 0.2f)
        {
            return;
        }
        int dx = Math.Abs(x) < 0.2f ? 0 : (int)(x * MathF.Pow(magnitude, 1.5f) * 10);
        int dy = Math.Abs(y) < 0.2f ? 0 : (int)(y * MathF.Pow(magnitude, 1.5f) * 10);
        _x11?.Scroll(dx, dy);
    }


    private static void ButtonClick(GamepadState state)
    {
        GamepadButtons changed = state.Buttons ^ _lastGamepadButtons;
        if (changed.HasFlag(GamepadButtons.A))
        {
            if (state.Buttons.HasFlag(GamepadButtons.A))
            {
                _x11?.MouseLeftButtonDown();
            }
            else
            {
                _x11?.MouseLeftButtonUp();
            }
        }
        if (changed.HasFlag(GamepadButtons.B))
        {
            if (state.Buttons.HasFlag(GamepadButtons.B))
            {
                _x11?.MouseRightButtonDown();
            }
            else
            {
                _x11?.MouseRightButtonUp();
            }
        }
        KeyEvent(changed, state.Buttons, GamepadButtons.X, VirtualKey.Enter);
        KeyEvent(changed, state.Buttons, GamepadButtons.Y, VirtualKey.Escape);
        KeyEvent(changed, state.Buttons, GamepadButtons.DPadLeft, VirtualKey.Left);
        KeyEvent(changed, state.Buttons, GamepadButtons.DPadUp, VirtualKey.Up);
        KeyEvent(changed, state.Buttons, GamepadButtons.DPadRight, VirtualKey.Right);
        KeyEvent(changed, state.Buttons, GamepadButtons.DPadDown, VirtualKey.Down);
    }


    /// <summary>
    /// 把 Win32 语义的 <see cref="VirtualKey"/> 转成注入层使用的 <see cref="VirtualKeyCode"/>。
    /// 上游只有一个 <c>Windows.System.VirtualKey</c>；本仓库按职责拆两套枚举（读法见 docs/09 «手柄增强»），
    /// 两者数值等价，此方法只做边界处的显式转换，避免隐式混用。
    /// </summary>
    private static VirtualKeyCode ToCode(VirtualKey virtualKey) => (VirtualKeyCode)(ushort)virtualKey;


    private static void KeyEvent(GamepadButtons changedButtons, GamepadButtons currentButtons, GamepadButtons keyButton, VirtualKey virtualKey)
    {
        if (changedButtons.HasFlag(keyButton))
        {
            if (currentButtons.HasFlag(keyButton))
            {
                _x11?.KeyDown(virtualKey);
            }
            else
            {
                _x11?.KeyUp(virtualKey);
            }
        }
    }


    private static void SwitchWindow(GamepadState state, long currentTimestamp)
    {
        GamepadButtons current = state.Buttons & (GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder);
        GamepadButtons changed = _lastShoulderButtons ^ current;
        if (changed != 0 && current == 0)
        {
            _x11?.KeyUp(VirtualKey.Menu);
        }
        if (changed.HasFlag(GamepadButtons.LeftShoulder) && current.HasFlag(GamepadButtons.LeftShoulder))
        {
            _x11?.KeyDown(VirtualKey.Menu);
            _x11?.ModifiedKeyStroke([ToCode(VirtualKey.Shift)], [ToCode(VirtualKey.Tab)]);
            _lastSwitchWindowTimestamp = currentTimestamp;
        }
        else if (changed.HasFlag(GamepadButtons.RightShoulder) && current.HasFlag(GamepadButtons.RightShoulder))
        {
            _x11?.KeyDown(VirtualKey.Menu);
            _x11?.ModifiedKeyStroke([], [ToCode(VirtualKey.Tab)]);
            _lastSwitchWindowTimestamp = currentTimestamp;
        }
        else if (changed.HasFlag(GamepadButtons.LeftShoulder) || changed.HasFlag(GamepadButtons.RightShoulder))
        {
            _lastSwitchWindowTimestamp = currentTimestamp;
        }
        if (current != 0 && currentTimestamp - _lastSwitchWindowTimestamp > 400)
        {
            if (current.HasFlag(GamepadButtons.LeftShoulder))
            {
                _x11?.ModifiedKeyStroke([ToCode(VirtualKey.Shift)], [ToCode(VirtualKey.Tab)]);
            }
            else if (current.HasFlag(GamepadButtons.RightShoulder))
            {
                _x11?.ModifiedKeyStroke([], [ToCode(VirtualKey.Tab)]);
            }
            _lastSwitchWindowTimestamp = currentTimestamp;
        }
        _lastShoulderButtons = current;
    }


    #endregion



    #region Xbox Game Bar


    public static void DisableGamepadGuideButtonForGameBar()
    {
        // 上游：HKCU\Software\Microsoft\GameBar\UseNexusForGameBarEnabled = 0。Linux 无 Xbox Game Bar（docs/09）。
        Serilog.Log.Debug("GamepadController: DisableGamepadGuideButtonForGameBar 在 Linux 无对应物（No-op）。");
    }



    public static void RestoreGamepadGuideButtonForGameBar()
    {
        Serilog.Log.Debug("GamepadController: RestoreGamepadGuideButtonForGameBar 在 Linux 无对应物（No-op）。");
    }



    public static void DisableGamepadGuideButtonForGameBarBecauseOfGameStart()
    {
        if (GamepadGuideButtonMode is 1 && RunningGameCount() > 0)
        {
            DisableGamepadGuideButtonForGameBar();
        }
    }



    public static void RestoreGamepadGuideButtonForGameBarBecauseOfGameExit()
    {
        if (GamepadGuideButtonMode is 1 && RunningGameCount() == 0)
        {
            RestoreGamepadGuideButtonForGameBar();
        }
    }



    public static void EnableXboxGameBarCapture()
    {
        // 上游：HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR\AppCaptureEnabled = 1（注意上游方法名拼写 Disbale）。
        Serilog.Log.Debug("GamepadController: EnableXboxGameBarCapture 在 Linux 无对应物（No-op）。");
    }



    public static void DisbaleXboxGameBarCapture()
    {
        Serilog.Log.Debug("GamepadController: DisbaleXboxGameBarCapture 在 Linux 无对应物（No-op）。");
    }


    #endregion



    /// <summary>
    /// 最近一帧的手柄状态（供 <see cref="SimulateInputTipsWindow"/> 做准星遮罩动画与提示展开，
    /// 对应上游窗口里直接调 <c>GamepadController.GameInputInstance.GetCurrentReading</c>）。
    /// </summary>
    internal static bool TryGetLatestState(out GamepadState state)
    {
        state = _latestState;
        return _latestStateValid && _controller?.HasController == true;
    }



    #region Simulate Input Window


    public static void ShowSimulateInputTipsWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                _simulateInputTipsWindow ??= new SimulateInputTipsWindow();
                _simulateInputTipsWindow.Activate();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "GamepadController: 显示模拟输入提示窗口失败");
            }
        });
    }



    public static void HideSimulateInputTipsWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _simulateInputTipsWindow?.Hide();
        });
    }


    #endregion



}
