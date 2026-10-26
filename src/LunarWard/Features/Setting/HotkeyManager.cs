using Avalonia.Input;

namespace LunarWard.Features.Setting;

/// <summary>
/// 全局热键管理（Starward <c>Features/Setting/HotkeyManager.cs</c> 的 Avalonia/Linux 等价物）。
/// </summary>
/// <remarks>
/// <para>
/// 公开形状与上游一致：<see cref="ShowMainWindow"/>/<see cref="ScreenshotCapture"/> 两个
/// <see cref="HotkeyInfo"/>（44444 / 44445）、<see cref="InitializeHotkey"/>、<see cref="InitializeHotkeyInput"/>、
/// <see cref="RegisterHotkey"/>、<see cref="UnregisterHotkey"/>、<see cref="DeleteHotkey"/>、<see cref="GetHotkeyInfo"/>。
/// </para>
/// <para>
/// Linux 差异：没有跨桌面会话的 <c>RegisterHotKey</c> 等价 API（X11 全局抓键在 Wayland 下普遍被禁），
/// 因此 <see cref="RegisterHotkey"/> 只持久化配置并返回 <see cref="HotkeyRegisterResult.NotSupported"/>，
/// 由页面把输入框置为 Warning 状态；组合键的「录制 + 保存」本身是可用的，触发留给合成器快捷键。
/// 配置沿用上游格式（<c>"修饰键位+虚拟键码"</c> 的十进制串，<c>"0"</c> 表示已删除），
/// 存放在 <c>AppConfig.ShowMainWindowHotkey / ScreenshotCaptureHotkey</c>（与上游同字段）。
/// </para>
/// </remarks>
internal static class HotkeyManager
{


    public static HotkeyInfo ShowMainWindow { get; } = new("show_main_window_hotkey", 44444, KeyModifiers.Alt, Key.S);


    public static HotkeyInfo ScreenshotCapture { get; } = new("screenshot_capture_hotkey", 44445, KeyModifiers.Alt, Key.D);



    /// <summary>读取配置并尝试注册（Linux 上只会更新内存状态）。</summary>
    public static void InitializeHotkey(nint hwnd)
    {
        foreach (var item in new[] { ShowMainWindow, ScreenshotCapture })
        {
            if (TryParseConfig(ReadConfig(item.ConfigSetting), out var modifiers, out var key))
            {
                item.Modifiers = modifiers;
                item.Key = key;
            }
            else
            {
                item.Modifiers = item.DefaultModifiers;
                item.Key = item.DefaultKey;
            }
            RegisterHotkey(hwnd, item.Id, item.Modifiers, item.Key);
        }
    }


    /// <summary>把配置里的快捷键填进输入框；未配置时填默认值（同上游）。</summary>
    public static void InitializeHotkeyInput(HotkeyInput hotkeyInput)
    {
        if (GetHotkeyInfo(hotkeyInput.HotkeyId) is not { } info)
        {
            return;
        }

        string? stored = ReadConfig(info.ConfigSetting);
        if (TryParseConfig(stored, out var modifiers, out var key))
        {
            hotkeyInput.SetHotkey(modifiers, key);
        }
        else if (stored is null)
        {
            hotkeyInput.SetHotkey(info.DefaultModifiers, info.DefaultKey);
        }

        hotkeyInput.State = info.Error is HotkeyRegisterResult.Succeeded
            ? HoykeyInputState.None
            : HoykeyInputState.Warning;
    }



    /// <summary>
    /// 注册/保存快捷键。Linux 上不真正注册：保存配置并返回 <see cref="HotkeyRegisterResult.NotSupported"/>。
    /// </summary>
    public static HotkeyRegisterResult RegisterHotkey(nint hwnd, int id, KeyModifiers modifiers, Key key)
    {
        if (GetHotkeyInfo(id) is not { } info)
        {
            return HotkeyRegisterResult.BadArguments;
        }

        var error = modifiers == KeyModifiers.None && key == Key.None
            ? HotkeyRegisterResult.Succeeded
            : HotkeyRegisterResult.NotSupported;

        bool changed = info.Modifiers != modifiers || info.Key != key;
        info.Modifiers = modifiers;
        info.Key = key;
        info.IsRegistered = error is HotkeyRegisterResult.Succeeded && (modifiers != KeyModifiers.None || key != Key.None);
        info.Error = error;

        if (changed)
        {
            WriteConfig(info.ConfigSetting, error is HotkeyRegisterResult.Succeeded && key is Key.None
                ? "0"
                : $"{HotkeyInput.ToWin32Modifiers(modifiers)}+{HotkeyInput.ToWin32VirtualKey(key)}");
        }

        return error;
    }


    public static HotkeyRegisterResult UnregisterHotkey(nint hwnd, int id)
    {
        if (GetHotkeyInfo(id) is { } info)
        {
            info.IsRegistered = false;
            info.Error = HotkeyRegisterResult.Succeeded;
        }
        return HotkeyRegisterResult.Succeeded;
    }


    public static HotkeyRegisterResult DeleteHotkey(nint hwnd, int id)
    {
        if (GetHotkeyInfo(id) is { } info)
        {
            info.Modifiers = KeyModifiers.None;
            info.Key = Key.None;
            info.IsRegistered = false;
            info.Error = HotkeyRegisterResult.Succeeded;
            WriteConfig(info.ConfigSetting, "0");
        }
        return HotkeyRegisterResult.Succeeded;
    }


    public static HotkeyInfo? GetHotkeyInfo(int id)
    {
        return id switch
        {
            44444 => ShowMainWindow,
            44445 => ScreenshotCapture,
            _ => null,
        };
    }



    public static bool TryParseConfig(string? value, out KeyModifiers modifiers, out Key key)
    {
        modifiers = KeyModifiers.None;
        key = Key.None;
        if (string.IsNullOrWhiteSpace(value) || value.Trim() is "0")
        {
            return false;
        }

        string[] splits = value.Split('+');
        if (splits.Length != 2
            || !uint.TryParse(splits[0].Trim(), out uint fsModifiers)
            || !uint.TryParse(splits[1].Trim(), out uint virtualKey))
        {
            return false;
        }

        modifiers = HotkeyInput.FromWin32Modifiers(fsModifiers);
        key = HotkeyInput.FromWin32VirtualKey(virtualKey);
        return key is not Key.None;
    }



    /// <summary>读取配置（上游用 <c>AppConfig.ShowMainWindowHotkey / ScreenshotCaptureHotkey</c>）。</summary>
    private static string? ReadConfig(string name)
        => name switch
        {
            "show_main_window_hotkey" => AppConfig.ShowMainWindowHotkey,
            "screenshot_capture_hotkey" => AppConfig.ScreenshotCaptureHotkey,
            _ => null,
        };


    /// <summary>写回配置（<see cref="AppConfig"/> 的 <c>Setting</c> 表，立即持久化）。</summary>
    private static void WriteConfig(string name, string value)
    {
        switch (name)
        {
            case "show_main_window_hotkey":
                AppConfig.ShowMainWindowHotkey = value;
                break;
            case "screenshot_capture_hotkey":
                AppConfig.ScreenshotCaptureHotkey = value;
                break;
        }
    }

}



/// <summary>注册结果（上游是 Vanara 的 <c>Win32Error</c>，Linux 上只需区分这几种）。</summary>
internal enum HotkeyRegisterResult
{
    Succeeded,
    NotSupported,
    BadArguments,
}



/// <summary>单个全局热键的配置与运行时状态，字段与上游 <c>HotkeyManager.HotkeyInfo</c> 对应。</summary>
internal sealed class HotkeyInfo
{

    public string ConfigSetting { get; init; }

    public int Id { get; init; }

    public KeyModifiers Modifiers { get; set; }

    public Key Key { get; set; }

    public bool IsRegistered { get; set; }

    public HotkeyRegisterResult Error { get; set; }

    public KeyModifiers DefaultModifiers { get; init; }

    public Key DefaultKey { get; init; }


    public HotkeyInfo(string configSetting, int id, KeyModifiers defaultModifiers, Key defaultKey)
    {
        ConfigSetting = configSetting;
        Id = id;
        DefaultModifiers = defaultModifiers;
        DefaultKey = defaultKey;
    }

}
