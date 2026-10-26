using System;

namespace LunarWard.Features.GamepadControl;

/// <summary>
/// Windows <c>Windows.System.VirtualKey</c> 的同值枚举（本移植的键位映射表用它做键，移植时只把类型名换掉）。
/// </summary>
/// <remarks>
/// 取值与 Win32 虚拟键码一致，与上游 <c>Starward</c> 用到的成员一一对应（<c>docs/09-LINUX-DIVERGENCES.md</c>）。
/// 上游在标点处直接写 <c>(VirtualKey)192</c> 这类强制转换，这里同样保留裸数值转换。
/// </remarks>
internal enum VirtualKey : ushort
{
    Back = 0x08,
    Tab = 0x09,
    Enter = 0x0D,
    Shift = 0x10,
    Control = 0x11,
    Menu = 0x12,
    Pause = 0x13,
    CapitalLock = 0x14,
    Escape = 0x1B,
    Space = 0x20,
    PageUp = 0x21,
    PageDown = 0x22,
    End = 0x23,
    Home = 0x24,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    Print = 0x2C,
    Insert = 0x2D,
    Delete = 0x2E,
    Number0 = 0x30,
    Number1 = 0x31,
    Number2 = 0x32,
    Number3 = 0x33,
    Number4 = 0x34,
    Number5 = 0x35,
    Number6 = 0x36,
    Number7 = 0x37,
    Number8 = 0x38,
    Number9 = 0x39,
    A = 0x41,
    B = 0x42,
    C = 0x43,
    D = 0x44,
    E = 0x45,
    F = 0x46,
    G = 0x47,
    H = 0x48,
    I = 0x49,
    J = 0x4A,
    K = 0x4B,
    L = 0x4C,
    M = 0x4D,
    N = 0x4E,
    O = 0x4F,
    P = 0x50,
    Q = 0x51,
    R = 0x52,
    S = 0x53,
    T = 0x54,
    U = 0x55,
    V = 0x56,
    W = 0x57,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A,
    LeftWindows = 0x5B,
    RightWindows = 0x5C,
    NumberPad0 = 0x60,
    NumberPad1 = 0x61,
    NumberPad2 = 0x62,
    NumberPad3 = 0x63,
    NumberPad4 = 0x64,
    NumberPad5 = 0x65,
    NumberPad6 = 0x66,
    NumberPad7 = 0x67,
    NumberPad8 = 0x68,
    NumberPad9 = 0x69,
    Multiply = 0x6A,
    Add = 0x6B,
    Subtract = 0x6D,
    Decimal = 0x6E,
    Divide = 0x6F,
    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,
    NumberKeyLock = 0x90,
    Scroll = 0x91,
}


/// <summary>
/// 上游 <c>H.InputSimulator.VirtualKeyCode</c> 的同值枚举（键位映射表的值类型）。
/// </summary>
internal enum VirtualKeyCode : ushort
{
    Back = 0x08,
    Tab = 0x09,
    Enter = 0x0D,
    Shift = 0x10,
    Control = 0x11,
    Alt = 0x12,
    Menu = 0x12,
    Pause = 0x13,
    Capital = 0x14,
    Escape = 0x1B,
    Space = 0x20,
    PageUp = 0x21,
    PageDown = 0x22,
    End = 0x23,
    Home = 0x24,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    PrintScreen = 0x2C,
    Insert = 0x2D,
    Delete = 0x2E,
    Key0 = 0x30,
    Key1 = 0x31,
    Key2 = 0x32,
    Key3 = 0x33,
    Key4 = 0x34,
    Key5 = 0x35,
    Key6 = 0x36,
    Key7 = 0x37,
    Key8 = 0x38,
    Key9 = 0x39,
    A = 0x41,
    B = 0x42,
    C = 0x43,
    D = 0x44,
    E = 0x45,
    F = 0x46,
    G = 0x47,
    H = 0x48,
    I = 0x49,
    J = 0x4A,
    K = 0x4B,
    L = 0x4C,
    M = 0x4D,
    N = 0x4E,
    O = 0x4F,
    P = 0x50,
    Q = 0x51,
    R = 0x52,
    S = 0x53,
    T = 0x54,
    U = 0x55,
    V = 0x56,
    W = 0x57,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A,
    LWin = 0x5B,
    RWin = 0x5C,
    NumPad0 = 0x60,
    NumPad1 = 0x61,
    NumPad2 = 0x62,
    NumPad3 = 0x63,
    NumPad4 = 0x64,
    NumPad5 = 0x65,
    NumPad6 = 0x66,
    NumPad7 = 0x67,
    NumPad8 = 0x68,
    NumPad9 = 0x69,
    Multiply = 0x6A,
    Add = 0x6B,
    Subtract = 0x6D,
    Decimal = 0x6E,
    Divide = 0x6F,
    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,
    NumLock = 0x90,
    Scroll = 0x91,
}


/// <summary>
/// 上游 <c>SharpGameInput.V0.GameInputGamepadButtons</c> 的同值标志枚举。
/// </summary>
[Flags]
internal enum GamepadButtons
{
    None = 0,
    Menu = 1,
    View = 2,
    A = 4,
    B = 8,
    X = 16,
    Y = 32,
    DPadUp = 64,
    DPadDown = 128,
    DPadLeft = 256,
    DPadRight = 512,
    LeftShoulder = 1024,
    RightShoulder = 2048,
    LeftThumbstick = 4096,
    RightThumbstick = 8192,
    Gamepad = 16384,
    /// <summary>系统键（上游由 <c>IGameInput.RegisterSystemButtonCallback</c> 单独回调，这里合进状态里）。</summary>
    Guide = 0x40000000,
    /// <summary>Xbox 系列手柄的 Share 键（SDL 映射为 <c>MISC1</c>）。</summary>
    Share = 0x20000000,
}


/// <summary>
/// 上游 <c>SharpGameInput.V0.GameInputGamepadState</c> 的同形结构（摇杆为 -1..1 的浮点，扳机 0..1）。
/// </summary>
internal struct GamepadState
{
    public GamepadButtons Buttons;
    public float LeftThumbstickX;
    public float LeftThumbstickY;
    public float RightThumbstickX;
    public float RightThumbstickY;
    public float LeftTrigger;
    public float RightTrigger;
}
