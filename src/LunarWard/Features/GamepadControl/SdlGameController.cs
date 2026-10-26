using System;
using System.Runtime.InteropServices;

namespace LunarWard.Features.GamepadControl;

/// <summary>
/// SDL2（<c>libSDL2-2.0.so.0</c>）的手柄后端：替代上游 <c>SharpGameInput.V0.IGameInput</c>。
/// </summary>
/// <remarks>
/// 选型理由见 <see cref="GamepadController"/> 的类注释：<c>SDL_GameController</c> 提供与
/// <c>Windows.Gaming.Input.Gamepad</c> 同级的标准化抽象（按键/轴映射 DB + 热插拔 + Guide/Share 键），
/// 比直接读 evdev 更接近上游语义。
/// 绑定时不使用 <c>[DllImport]</c>，而是运行时 <see cref="NativeLibrary.TryLoad(string, out IntPtr)"/> 探测
/// （候选名 <c>libSDL2-2.0.so.0</c> / <c>libSDL2-2.0.so</c> / <c>libSDL2.so</c>），缺失时构造失败但<b>不抛异常</b>，
/// 由 <see cref="ErrorMessage"/> 给出可读原因，交给 <see cref="GamepadController.UnavailableReason"/> 展示。
/// 所有 SDL 调用都在同一个线程（<see cref="GamepadController"/> 的 <c>System.Timers.Timer</c> 回调）里发生，
/// 避免 SDL 的线程限制。
/// </remarks>
internal sealed class SdlGameController
{

    private const uint SDL_INIT_JOYSTICK = 0x00000200;
    private const uint SDL_INIT_GAMECONTROLLER = 0x00002000;
    private const uint SDL_INIT_EVENTS = 0x00004000;

    private const uint SDL_CONTROLLERDEVICEADDED = 0x653;
    private const uint SDL_CONTROLLERDEVICEREMOVED = 0x654;
    private const uint SDL_CONTROLLERDEVICEREMAPPED = 0x655;
    private const uint SDL_JOYDEVICEADDED = 0x605;
    private const uint SDL_JOYDEVICEREMOVED = 0x606;

    private const int SDL_CONTROLLER_BUTTON_A = 0;
    private const int SDL_CONTROLLER_BUTTON_B = 1;
    private const int SDL_CONTROLLER_BUTTON_X = 2;
    private const int SDL_CONTROLLER_BUTTON_Y = 3;
    private const int SDL_CONTROLLER_BUTTON_BACK = 4;
    private const int SDL_CONTROLLER_BUTTON_GUIDE = 5;
    private const int SDL_CONTROLLER_BUTTON_START = 6;
    private const int SDL_CONTROLLER_BUTTON_LEFTSTICK = 7;
    private const int SDL_CONTROLLER_BUTTON_RIGHTSTICK = 8;
    private const int SDL_CONTROLLER_BUTTON_LEFTSHOULDER = 9;
    private const int SDL_CONTROLLER_BUTTON_RIGHTSHOULDER = 10;
    private const int SDL_CONTROLLER_BUTTON_DPAD_UP = 11;
    private const int SDL_CONTROLLER_BUTTON_DPAD_DOWN = 12;
    private const int SDL_CONTROLLER_BUTTON_DPAD_LEFT = 13;
    private const int SDL_CONTROLLER_BUTTON_DPAD_RIGHT = 14;
    private const int SDL_CONTROLLER_BUTTON_MISC1 = 15;

    private const int SDL_CONTROLLER_AXIS_LEFTX = 0;
    private const int SDL_CONTROLLER_AXIS_LEFTY = 1;
    private const int SDL_CONTROLLER_AXIS_RIGHTX = 2;
    private const int SDL_CONTROLLER_AXIS_RIGHTY = 3;
    private const int SDL_CONTROLLER_AXIS_TRIGGERLEFT = 4;
    private const int SDL_CONTROLLER_AXIS_TRIGGERRIGHT = 5;


    private IntPtr _controller;

    private bool _guidePressed;

    private bool _sharePressed;

    private bool _initialButtonStateLogged;


    public SdlGameController()
    {
        if (!Sdl.TryLoad(out string? error))
        {
            ErrorMessage = error;
            return;
        }
        try
        {
            if (Sdl.Init(SDL_INIT_JOYSTICK | SDL_INIT_GAMECONTROLLER | SDL_INIT_EVENTS) != 0)
            {
                ErrorMessage = Sdl.GetError() ?? "SDL_Init 失败";
                return;
            }
            Initialized = true;
            OpenFirstController();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }


    public bool Initialized { get; }


    public string? ErrorMessage { get; }


    public bool HasController => _controller != IntPtr.Zero;



    private void OpenFirstController()
    {
        int count = Sdl.NumJoysticks();
        for (int i = 0; i < count; i++)
        {
            if (!Sdl.IsGameController(i))
            {
                continue;
            }
            IntPtr controller = Sdl.GameControllerOpen(i);
            if (controller != IntPtr.Zero)
            {
                _controller = controller;
                return;
            }
        }
    }



    private void CloseController()
    {
        if (_controller != IntPtr.Zero)
        {
            Sdl.GameControllerClose(_controller);
            _controller = IntPtr.Zero;
        }
    }



    /// <summary>
    /// 抽 SDL 事件：只有手柄热插拔会让它通知 <paramref name="deviceChanged"/>，
    /// Guide/Share 键的按下/松开分别触发 <paramref name="guideDown"/>/<paramref name="guideUp"/>/<paramref name="shareDown"/>
    /// （上游对应 <c>RegisterDeviceCallback</c>/<c>RegisterSystemButtonCallback</c>）。
    /// </summary>
    public void PumpEvents(Action<bool, bool> deviceChanged, Action guideDown, Action guideUp, Action shareDown)
    {
        if (!Initialized)
        {
            return;
        }
        while (Sdl.PollEvent(out SdlEvent e) != 0)
        {
            switch (e.Type)
            {
                case SDL_CONTROLLERDEVICEADDED:
                case SDL_JOYDEVICEADDED:
                    {
                        if (!HasController)
                        {
                            IntPtr controller = Sdl.GameControllerOpen(e.Which);
                            if (controller != IntPtr.Zero)
                            {
                                _controller = controller;
                                deviceChanged(true, true);
                            }
                        }
                        break;
                    }
                case SDL_CONTROLLERDEVICEREMOVED:
                case SDL_JOYDEVICEREMOVED:
                    {
                        if (HasController)
                        {
                            // SDL_CONTROLLERDEVICEREMOVED 的 which 是 instance id，与 open 时的 index 不同；
                            // 直接按“手柄断开”处理并重新扫描。
                            CloseController();
                            deviceChanged(false, false);
                            OpenFirstController();
                        }
                        break;
                    }
                case SDL_CONTROLLERDEVICEREMAPPED:
                    break;
            }
        }
        if (HasController)
        {
            bool guide = Sdl.GameControllerGetButton(_controller, SDL_CONTROLLER_BUTTON_GUIDE) != 0;
            if (guide != _guidePressed)
            {
                _guidePressed = guide;
                if (guide)
                {
                    guideDown();
                }
                else
                {
                    guideUp();
                }
            }
            bool share = Sdl.GameControllerGetButton(_controller, SDL_CONTROLLER_BUTTON_MISC1) != 0;
            if (share && !_sharePressed)
            {
                shareDown();
            }
            _sharePressed = share;
        }
    }



    public bool GetState(out GamepadState state)
    {
        state = default;
        if (_controller == IntPtr.Zero)
        {
            return false;
        }
        GamepadButtons buttons = GamepadButtons.None;
        if (IsPressed(SDL_CONTROLLER_BUTTON_START)) buttons |= GamepadButtons.Menu;
        if (IsPressed(SDL_CONTROLLER_BUTTON_BACK)) buttons |= GamepadButtons.View;
        if (IsPressed(SDL_CONTROLLER_BUTTON_A)) buttons |= GamepadButtons.A;
        if (IsPressed(SDL_CONTROLLER_BUTTON_B)) buttons |= GamepadButtons.B;
        if (IsPressed(SDL_CONTROLLER_BUTTON_X)) buttons |= GamepadButtons.X;
        if (IsPressed(SDL_CONTROLLER_BUTTON_Y)) buttons |= GamepadButtons.Y;
        if (IsPressed(SDL_CONTROLLER_BUTTON_DPAD_UP)) buttons |= GamepadButtons.DPadUp;
        if (IsPressed(SDL_CONTROLLER_BUTTON_DPAD_DOWN)) buttons |= GamepadButtons.DPadDown;
        if (IsPressed(SDL_CONTROLLER_BUTTON_DPAD_LEFT)) buttons |= GamepadButtons.DPadLeft;
        if (IsPressed(SDL_CONTROLLER_BUTTON_DPAD_RIGHT)) buttons |= GamepadButtons.DPadRight;
        if (IsPressed(SDL_CONTROLLER_BUTTON_LEFTSHOULDER)) buttons |= GamepadButtons.LeftShoulder;
        if (IsPressed(SDL_CONTROLLER_BUTTON_RIGHTSHOULDER)) buttons |= GamepadButtons.RightShoulder;
        if (IsPressed(SDL_CONTROLLER_BUTTON_LEFTSTICK)) buttons |= GamepadButtons.LeftThumbstick;
        if (IsPressed(SDL_CONTROLLER_BUTTON_RIGHTSTICK)) buttons |= GamepadButtons.RightThumbstick;
        if (IsPressed(SDL_CONTROLLER_BUTTON_GUIDE)) buttons |= GamepadButtons.Guide;
        if (IsPressed(SDL_CONTROLLER_BUTTON_MISC1)) buttons |= GamepadButtons.Share;
        state.Buttons = buttons;
        state.LeftThumbstickX = NormalizeAxis(SDL_CONTROLLER_AXIS_LEFTX);
        // SDL 的 Y 轴向下为正，GameInput 向上为正（上游 MouseMove 里用的是 -y）。
        state.LeftThumbstickY = -NormalizeAxis(SDL_CONTROLLER_AXIS_LEFTY);
        state.RightThumbstickX = NormalizeAxis(SDL_CONTROLLER_AXIS_RIGHTX);
        state.RightThumbstickY = -NormalizeAxis(SDL_CONTROLLER_AXIS_RIGHTY);
        state.LeftTrigger = NormalizeTrigger(SDL_CONTROLLER_AXIS_TRIGGERLEFT);
        state.RightTrigger = NormalizeTrigger(SDL_CONTROLLER_AXIS_TRIGGERRIGHT);
        if (!_initialButtonStateLogged)
        {
            _initialButtonStateLogged = true;
            Serilog.Log.Information("GamepadController: 手柄已连接，首帧按键位=0x{Buttons:X}", (int)buttons);
        }
        return true;
    }


    private bool IsPressed(int button)
    {
        return Sdl.GameControllerGetButton(_controller, button) != 0;
    }


    private float NormalizeAxis(int axis)
    {
        short value = Sdl.GameControllerGetAxis(_controller, axis);
        return value >= 0 ? value / 32767f : value / 32768f;
    }


    private float NormalizeTrigger(int axis)
    {
        short value = Sdl.GameControllerGetAxis(_controller, axis);
        return Math.Clamp(value / 32767f, 0f, 1f);
    }



    [StructLayout(LayoutKind.Explicit, Size = 56)]
    private struct SdlEvent
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(4)]
        public uint Timestamp;

        [FieldOffset(8)]
        public int Which;
    }



    /// <summary>
    /// SDL2 函数表：<see cref="NativeLibrary.TryLoad(string, out IntPtr)"/> 运行时绑定，缺失即整体不可用。
    /// </summary>
    private static unsafe class Sdl
    {

        private static bool _loaded;

        private static IntPtr _handle;

        private static string? _error;

        private static delegate* unmanaged[Cdecl]<uint, int> _init;

        private static delegate* unmanaged[Cdecl]<IntPtr> _getError;

        private static delegate* unmanaged[Cdecl]<out SdlEvent, int> _pollEvent;

        private static delegate* unmanaged[Cdecl]<int> _numJoysticks;

        private static delegate* unmanaged[Cdecl]<int, int> _isGameController;

        private static delegate* unmanaged[Cdecl]<int, IntPtr> _gameControllerOpen;

        private static delegate* unmanaged[Cdecl]<IntPtr, void> _gameControllerClose;

        private static delegate* unmanaged[Cdecl]<IntPtr, int, byte> _gameControllerGetButton;

        private static delegate* unmanaged[Cdecl]<IntPtr, int, short> _gameControllerGetAxis;


        public static bool TryLoad(out string? error)
        {
            if (_loaded)
            {
                error = _error;
                return _handle != IntPtr.Zero;
            }
            string[] candidates = ["libSDL2-2.0.so.0", "libSDL2-2.0.so", "libSDL2.so"];
            foreach (string candidate in candidates)
            {
                if (NativeLibrary.TryLoad(candidate, out _handle))
                {
                    break;
                }
            }
            if (_handle == IntPtr.Zero)
            {
                _loaded = true;
                _error = "未找到 libSDL2-2.0.so.0（请安装 SDL2，例如 sudo apt install libsdl2-2.0-0）";
                error = _error;
                return false;
            }
            try
            {
                _init = (delegate* unmanaged[Cdecl]<uint, int>)NativeLibrary.GetExport(_handle, "SDL_Init");
                _getError = (delegate* unmanaged[Cdecl]<IntPtr>)NativeLibrary.GetExport(_handle, "SDL_GetError");
                _pollEvent = (delegate* unmanaged[Cdecl]<out SdlEvent, int>)NativeLibrary.GetExport(_handle, "SDL_PollEvent");
                _numJoysticks = (delegate* unmanaged[Cdecl]<int>)NativeLibrary.GetExport(_handle, "SDL_NumJoysticks");
                _isGameController = (delegate* unmanaged[Cdecl]<int, int>)NativeLibrary.GetExport(_handle, "SDL_IsGameController");
                _gameControllerOpen = (delegate* unmanaged[Cdecl]<int, IntPtr>)NativeLibrary.GetExport(_handle, "SDL_GameControllerOpen");
                _gameControllerClose = (delegate* unmanaged[Cdecl]<IntPtr, void>)NativeLibrary.GetExport(_handle, "SDL_GameControllerClose");
                _gameControllerGetButton = (delegate* unmanaged[Cdecl]<IntPtr, int, byte>)NativeLibrary.GetExport(_handle, "SDL_GameControllerGetButton");
                _gameControllerGetAxis = (delegate* unmanaged[Cdecl]<IntPtr, int, short>)NativeLibrary.GetExport(_handle, "SDL_GameControllerGetAxis");
            }
            catch (EntryPointNotFoundException ex)
            {
                _loaded = true;
                _error = $"SDL2 动态库缺少必要符号（{ex.Message}）";
                error = _error;
                return false;
            }
            _loaded = true;
            error = null;
            return true;
        }


        public static int Init(uint flags) => _init(flags);

        public static string? GetError()
        {
            IntPtr ptr = _getError();
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
        }

        public static int PollEvent(out SdlEvent e) => _pollEvent(out e);

        public static int NumJoysticks() => _numJoysticks();

        public static bool IsGameController(int index) => _isGameController(index) != 0;

        public static IntPtr GameControllerOpen(int index) => _gameControllerOpen(index);

        public static void GameControllerClose(IntPtr controller) => _gameControllerClose(controller);

        public static byte GameControllerGetButton(IntPtr controller, int button) => _gameControllerGetButton(controller, button);

        public static short GameControllerGetAxis(IntPtr controller, int axis) => _gameControllerGetAxis(controller, axis);

    }


}
