using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LunarWard.Language;
using LunarWard.Localization;

namespace LunarWard.Features.Setting;

/// <summary>
/// 快捷键录制控件（Starward <c>HotkeyInput.xaml</c> / <c>HotkeyInput.xaml.cs</c> 的 Avalonia 等价物）。
/// </summary>
/// <remarks>
/// <para>
/// 公开面与上游一致：<see cref="WindowHandle"/>、<see cref="HotkeyId"/>、<see cref="Modifiers"/>、
/// <see cref="Key"/>、<see cref="State"/>、<see cref="SetHotkey(KeyModifiers, Key)"/>、
/// <see cref="HotkeyEditing"/>/<see cref="HotkeyEditFinished"/>/<see cref="HotkeyDeleted"/> 事件、
/// <see cref="HoykeyInputState"/> 状态机、以及静态的 <see cref="GetHotkeyText(uint, uint)"/> /
/// <see cref="IsHotkeyAvaliable(uint, uint)"/>。
/// </para>
/// <para>
/// Linux 差异只有捕获方式：上游用 <c>GetKeyState</c> + <c>KeyDown/KeyUp</c>，LunarWard 用 Avalonia
/// 路由事件（<see cref="Grid_EditHotkey"/> 以隧道方式接收，避免被内部按钮吃掉）；键位文本沿用上游的
/// <c>"Win + Ctrl + Alt + Shift + Key"</c> 格式，配置里仍存 Win32 的「修饰键位+虚拟键码」整数，
/// 与上游 <c>AppConfig</c> 格式兼容。
/// </para>
/// </remarks>
public sealed partial class HotkeyInput : UserControl
{

    private KeyModifiers _editingModifiers;

    private Key _editingKey;

    private bool _isEditing;

    private HoykeyInputState _state;



    public HotkeyInput()
    {
        InitializeComponent();
        // 文案/tooltip 在代码里赋值（本控件不固定 DataContext，见 .axaml 顶部注释）。
        TextBlock_ClickToSetHotkey.Text = Lang.HotkeyInput_ClickToSetShortcutKeys;
        ToolTip.SetTip(Button_DeleteHotkey, Strings["Settings.Hotkey.Delete"]);
        ToolTip.SetTip(Button_CancelEdit, Strings["Settings.Hotkey.CancelEdit"]);
        // 隧道阶段接收键盘事件：内部 Button 获得焦点时也能录制。
        Grid_EditHotkey.AddHandler(KeyDownEvent, Grid_EditHotkey_KeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        Grid_EditHotkey.AddHandler(KeyUpEvent, Grid_EditHotkey_KeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
    }



    /// <summary>上游是 Win32 窗口句柄；Linux 用 <c>0</c>（不使用全局热键）。</summary>
    public nint WindowHandle { get; set; }


    /// <summary>热键编号（44444 打开主窗口 / 44445 游戏截图），与上游一致。</summary>
    public int HotkeyId { get; set; }


    public KeyModifiers Modifiers { get; private set; } = KeyModifiers.None;


    public Key Key { get; private set; } = Key.None;


    public HoykeyInputState State
    {
        get => _state;
        set
        {
            _state = value;
            ApplyState(value);
        }
    }


    public string? EditingText { get; private set; }

    public string? HotkeyText { get; private set; }



    public event EventHandler<HotkeyInputEventArg>? HotkeyEditing;

    public event EventHandler<HotkeyEditFinishedEventArg>? HotkeyEditFinished;

    public event EventHandler<HotkeyInputEventArg>? HotkeyDeleted;



    /// <summary>设置已保存的快捷键并刷新文案；不可用时返回 <c>false</c>（同上游）。</summary>
    public bool SetHotkey(KeyModifiers modifiers, Key key)
    {
        if (!IsHotkeyAvaliable(modifiers, key))
        {
            return false;
        }

        Modifiers = modifiers;
        Key = key;
        UpdateText();
        State = HoykeyInputState.None;
        return true;
    }


    /// <summary>Win32 风格的重载（配置格式路径，同上游）。</summary>
    public bool SetHotkey(uint fsModifiers, uint key)
    {
        return SetHotkey(FromWin32Modifiers(fsModifiers), FromWin32VirtualKey(key));
    }


    private void ApplyState(HoykeyInputState value)
    {
        if (value is HoykeyInputState.None)
        {
            TextBlock_EditingText.IsVisible = false;
            TextBlock_HotkeyText_Warning.IsVisible = false;
            Button_Success.IsVisible = false;
            Button_CancelEdit.IsVisible = false;
            if (string.IsNullOrWhiteSpace(HotkeyText))
            {
                TextBlock_ClickToSetHotkey.IsVisible = true;
                TextBlock_HotkeyText.IsVisible = false;
                Button_DeleteHotkey.IsVisible = false;
            }
            else
            {
                TextBlock_ClickToSetHotkey.IsVisible = false;
                TextBlock_HotkeyText.IsVisible = true;
                Button_DeleteHotkey.IsVisible = true;
            }
        }
        else if (value is HoykeyInputState.Edit)
        {
            TextBlock_ClickToSetHotkey.IsVisible = false;
            TextBlock_HotkeyText.IsVisible = false;
            TextBlock_EditingText.IsVisible = true;
            TextBlock_HotkeyText_Warning.IsVisible = false;
            Button_Success.IsVisible = false;
            Button_DeleteHotkey.IsVisible = false;
            Button_CancelEdit.IsVisible = true;
        }
        else if (value is HoykeyInputState.Success)
        {
            TextBlock_ClickToSetHotkey.IsVisible = false;
            TextBlock_HotkeyText.IsVisible = true;
            TextBlock_EditingText.IsVisible = false;
            TextBlock_HotkeyText_Warning.IsVisible = false;
            Button_Success.IsVisible = true;
            Button_DeleteHotkey.IsVisible = false;
            Button_CancelEdit.IsVisible = false;
        }
        else if (value is HoykeyInputState.Warning)
        {
            TextBlock_ClickToSetHotkey.IsVisible = false;
            TextBlock_HotkeyText.IsVisible = false;
            TextBlock_EditingText.IsVisible = false;
            TextBlock_HotkeyText_Warning.IsVisible = true;
            Button_Success.IsVisible = false;
            Button_DeleteHotkey.IsVisible = true;
            Button_CancelEdit.IsVisible = false;
        }
    }



    private void UpdateText()
    {
        HotkeyText = GetHotkeyText(Modifiers, Key);
        EditingText = GetHotkeyText(_editingModifiers, _editingKey) ?? Lang.HotkeyInput_PressTheShortcutKeys;
        TextBlock_HotkeyText.Text = HotkeyText;
        TextBlock_HotkeyText_Warning.Text = HotkeyText;
        TextBlock_EditingText.Text = EditingText;
    }



    private void Button_EditHotkey_Click(object? sender, RoutedEventArgs e)
    {
        BeginEdit();
    }


    private void BeginEdit()
    {
        if (!_isEditing)
        {
            _isEditing = true;
            _editingModifiers = KeyModifiers.None;
            _editingKey = Key.None;
            HotkeyEditing?.Invoke(this, CreateEventArg());
        }

        UpdateText();
        State = HoykeyInputState.Edit;
        Grid_EditHotkey.Focus();
    }


    private void CancelEdit()
    {
        _isEditing = false;
        _editingModifiers = KeyModifiers.None;
        _editingKey = Key.None;
        UpdateText();
        State = HoykeyInputState.None;
    }


    private void Button_CancelEdit_Click(object? sender, RoutedEventArgs e)
    {
        CancelEdit();
    }


    private void Button_DeleteHotkey_Click(object? sender, RoutedEventArgs e)
    {
        var args = CreateEventArg();
        Modifiers = KeyModifiers.None;
        Key = Key.None;
        _isEditing = false;
        UpdateText();
        State = HoykeyInputState.None;
        HotkeyDeleted?.Invoke(this, args);
    }



    private void Grid_EditHotkey_KeyDown(object? sender, KeyEventArgs e)
    {
        if (IsModifierKey(e.Key))
        {
            // 只按修饰键时不结束录制，但要把修饰键反映到编辑文案里。
            _editingModifiers = e.KeyModifiers;
            UpdateText();
            TextBlock_EditingText.Text = EditingText;
            e.Handled = true;
            return;
        }

        if (!_isEditing)
        {
            BeginEdit();
        }

        _editingModifiers = e.KeyModifiers;
        _editingKey = e.Key;
        UpdateText();
        e.Handled = true;
    }


    private void Grid_EditHotkey_KeyUp(object? sender, KeyEventArgs e)
    {
        if (!_isEditing || IsModifierKey(e.Key))
        {
            return;
        }

        if (e.Key != _editingKey && _editingKey is not Key.None)
        {
            return;
        }

        FinishEdit();
        e.Handled = true;
    }


    private void Grid_EditHotkey_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_isEditing)
        {
            CancelEdit();
        }
    }


    private void FinishEdit()
    {
        var modifiers = _editingModifiers;
        var key = _editingKey;
        if (key is Key.None)
        {
            return;
        }

        bool avaliable = IsHotkeyAvaliable(modifiers, key);
        bool changed = modifiers != Modifiers || key != Key;

        var args = new HotkeyEditFinishedEventArg
        {
            WindowHandle = WindowHandle,
            HotkeyId = HotkeyId,
            Modifiers = modifiers,
            Key = key,
            fsModifiers = ToWin32Modifiers(modifiers),
            HotkeyAvaliable = avaliable,
            HotkeyChanged = changed,
        };

        _isEditing = false;
        if (avaliable)
        {
            Modifiers = modifiers;
            Key = key;
            UpdateText();
            State = HoykeyInputState.None;
        }
        else
        {
            // 组合键非法（例如只按了字母没按修饰键）：恢复原值，由页面决定是否提示。
            CancelEdit();
        }

        HotkeyEditFinished?.Invoke(this, args);
    }



    private HotkeyInputEventArg CreateEventArg()
    {
        return new HotkeyInputEventArg
        {
            WindowHandle = WindowHandle,
            HotkeyId = HotkeyId,
            Modifiers = Modifiers,
            Key = Key,
            fsModifiers = ToWin32Modifiers(Modifiers),
        };
    }



    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
    }



    public static bool IsHotkeyAvaliable(uint fsModifiers, uint key)
    {
        return IsHotkeyAvaliable(FromWin32Modifiers(fsModifiers), FromWin32VirtualKey(key));
    }


    public static bool IsHotkeyAvaliable(KeyModifiers modifiers, Key key)
    {
        if (modifiers == KeyModifiers.None && IsFunctionKey(key))
        {
            return true;
        }

        return modifiers != KeyModifiers.None && GetKeyName(key) is not null;
    }



    public static string? GetHotkeyText(uint fsModifiers, uint key)
    {
        return GetHotkeyText(FromWin32Modifiers(fsModifiers), FromWin32VirtualKey(key));
    }


    public static string? GetHotkeyText(KeyModifiers modifiers, Key key)
    {
        if (modifiers == KeyModifiers.None && IsFunctionKey(key))
        {
            return GetKeyName(key);
        }

        var sb = new System.Text.StringBuilder();
        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            sb.Append("Win + ");
        }
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            sb.Append("Ctrl + ");
        }
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            sb.Append("Alt + ");
        }
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            sb.Append("Shift + ");
        }

        if (GetKeyName(key) is { } name)
        {
            sb.Append(name);
        }
        else if (sb.Length == 0)
        {
            return null;
        }

        return sb.ToString();
    }


    private static bool IsFunctionKey(Key key)
    {
        return key is >= Key.F1 and <= Key.F24;
    }



    /// <summary>键位文本；不在上游 <c>AvaliableKeyDict</c> 等价表里的键返回 <c>null</c>。</summary>
    private static string? GetKeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return key.ToString();
        }
        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)key - (int)Key.D0).ToString();
        }
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return $"NumPad{(int)key - (int)Key.NumPad0}";
        }
        if (key is >= Key.F1 and <= Key.F24)
        {
            return key.ToString();
        }

        return key switch
        {
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Escape => "Esc",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemTilde => "`",
            _ => null,
        };
    }



    /// <summary>Win32 <c>MOD_ALT|MOD_CONTROL|MOD_SHIFT|MOD_WIN</c> → Avalonia 修饰键。</summary>
    internal static KeyModifiers FromWin32Modifiers(uint fsModifiers)
    {
        KeyModifiers modifiers = KeyModifiers.None;
        if ((fsModifiers & 0x1) != 0)
        {
            modifiers |= KeyModifiers.Alt;
        }
        if ((fsModifiers & 0x2) != 0)
        {
            modifiers |= KeyModifiers.Control;
        }
        if ((fsModifiers & 0x4) != 0)
        {
            modifiers |= KeyModifiers.Shift;
        }
        if ((fsModifiers & 0x8) != 0)
        {
            modifiers |= KeyModifiers.Meta;
        }
        return modifiers;
    }


    internal static uint ToWin32Modifiers(KeyModifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            result |= 0x1;
        }
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            result |= 0x2;
        }
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            result |= 0x4;
        }
        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            result |= 0x8;
        }
        return result;
    }



    /// <summary>Win32 虚拟键码 → Avalonia <see cref="Key"/>；表外返回 <see cref="Key.None"/>。</summary>
    internal static Key FromWin32VirtualKey(uint vk)
    {
        if (vk is >= 0x41 and <= 0x5A)
        {
            return (Key)((int)Key.A + (int)(vk - 0x41));
        }
        if (vk is >= 0x30 and <= 0x39)
        {
            return (Key)((int)Key.D0 + (int)(vk - 0x30));
        }
        if (vk is >= 0x70 and <= 0x87)
        {
            return (Key)((int)Key.F1 + (int)(vk - 0x70));
        }

        return vk switch
        {
            0x20 => Key.Space,
            0x0D => Key.Enter,
            0x1B => Key.Escape,
            0x09 => Key.Tab,
            0x08 => Key.Back,
            0x2E => Key.Delete,
            0x2D => Key.Insert,
            0x24 => Key.Home,
            0x23 => Key.End,
            0x21 => Key.PageUp,
            0x22 => Key.PageDown,
            0x25 => Key.Left,
            0x27 => Key.Right,
            0x26 => Key.Up,
            0x28 => Key.Down,
            0x6A => Key.Multiply,
            0x6B => Key.Add,
            _ => Key.None,
        };
    }



    /// <summary>Avalonia <see cref="Key"/> → Win32 虚拟键码（配置格式，同上游）。</summary>
    internal static uint ToWin32VirtualKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return (uint)(0x41 + (key - Key.A));
        }
        if (key is >= Key.D0 and <= Key.D9)
        {
            return (uint)(0x30 + (key - Key.D0));
        }
        if (key is >= Key.F1 and <= Key.F24)
        {
            return (uint)(0x70 + (key - Key.F1));
        }

        return key switch
        {
            Key.Space => 0x20,
            Key.Enter => 0x0D,
            Key.Escape => 0x1B,
            Key.Tab => 0x09,
            Key.Back => 0x08,
            Key.Delete => 0x2E,
            Key.Insert => 0x2D,
            Key.Home => 0x24,
            Key.End => 0x23,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.Left => 0x25,
            Key.Right => 0x27,
            Key.Up => 0x26,
            Key.Down => 0x28,
            _ => 0,
        };
    }


    private AppStrings Strings => AppStrings.Instance;

}




/// <summary>输入框四态（同上游 <c>HoykeyInputState</c>）。</summary>
public enum HoykeyInputState
{
    None,
    Edit,
    Success,
    Warning,
}



/// <summary>编辑/删除事件参数（同上游 <c>HotkeyInputEventArg</c>）。</summary>
public class HotkeyInputEventArg : EventArgs
{
    public required nint WindowHandle { get; init; }

    public required int HotkeyId { get; init; }

    public required KeyModifiers Modifiers { get; init; }

    public required Key Key { get; init; }

    public required uint fsModifiers { get; init; }
}



/// <summary>编辑完成事件参数（同上游 <c>HotkeyEditFinishedEventArg</c>）。</summary>
public class HotkeyEditFinishedEventArg : HotkeyInputEventArg
{
    public required bool HotkeyAvaliable { get; set; }

    public required bool HotkeyChanged { get; set; }
}
