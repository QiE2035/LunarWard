using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Frameworks;

namespace LunarWard.Features.Setting;

/// <summary>
/// 快捷键页（复刻 Starward <c>HotkeySetting.xaml</c> / <c>HotkeySetting.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// Starward 用 Win32 <c>RegisterHotKey</c> 注册两个全局热键（显示主窗口 / 游戏截图）。
/// Linux 没有跨桌面等价 API：LunarWard 的 <see cref="HotkeyManager"/> 只保存组合键、不注册，
/// 触发方式是在合成器里绑定（说明与桌面入口位置见下方 LunarWard 追加的段落）。
/// </remarks>
public sealed partial class HotkeySetting : PageBase
{

    public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private static AppServices Services => AppServices.Current;

    private static Localization.AppStrings Strings => Localization.AppStrings.Instance;

    /// <summary>错误/状态行（上游各设置页末尾的 InfoBar 文本）。</summary>
    public string? Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    private string? _status;

    private static Services.LogService Log => AppServices.Current.Log;
    public HotkeySetting()
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();

        WindowHandle = 0;
        HotkeyInput_ShowMainWindow.WindowHandle = WindowHandle;
        HotkeyInput_ScreenshotCapture.WindowHandle = WindowHandle;

        DesktopEntryPath = SettingShellActions.FindDesktopEntry();
        DesktopEntryCandidates = string.Join('\n', SettingShellActions.DesktopEntryCandidates());

        InitializeHotkeyInput();

        DataContext = this;
    }


    /// <summary>上游是 Win32 窗口句柄；Linux 上全局热键不可用，恒为 <c>0</c>。</summary>
    public nint WindowHandle { get; }


    /// <summary>已安装的 <c>lunarward.desktop</c>，找不到为 <c>null</c>。</summary>
    public string? DesktopEntryPath { get; }

    /// <summary>候选路径（找不到时用来告诉用户该放哪）。</summary>
    public string DesktopEntryCandidates { get; }

    public bool HasDesktopEntry => DesktopEntryPath is not null;


    /// <summary>对应上游 <c>HotkeySetting.InitializeHotkeyInput</c>。</summary>
    private void InitializeHotkeyInput()
    {
        HotkeyManager.InitializeHotkey(WindowHandle);
        HotkeyManager.InitializeHotkeyInput(HotkeyInput_ShowMainWindow);
        HotkeyManager.InitializeHotkeyInput(HotkeyInput_ScreenshotCapture);
    }



    private void HotkeyInput_HotkeyEditing(object? sender, HotkeyInputEventArg e)
    {
        HotkeyManager.UnregisterHotkey(e.WindowHandle, e.HotkeyId);
    }


    private void HotkeyInput_HotkeyDeleted(object? sender, HotkeyInputEventArg e)
    {
        Focus();
        HotkeyManager.DeleteHotkey(e.WindowHandle, e.HotkeyId);
        Status = null;
    }


    private void HotkeyInput_HotkeyEditFinished(object? sender, HotkeyEditFinishedEventArg e)
    {
        if (sender is not HotkeyInput input)
        {
            return;
        }

        if (!e.HotkeyAvaliable)
        {
            input.State = HoykeyInputState.Warning;
            return;
        }

        var result = HotkeyManager.RegisterHotkey(e.WindowHandle, e.HotkeyId, e.Modifiers, e.Key);
        switch (result)
        {
            case HotkeyRegisterResult.Succeeded:
                input.State = e.HotkeyChanged ? HoykeyInputState.Success : HoykeyInputState.None;
                Status = null;
                break;
            case HotkeyRegisterResult.NotSupported:
                // Linux：组合键已保存，但没有全局注册能力 —— 与上游注册失败一样显示 Warning。
                input.State = HoykeyInputState.Warning;
                Status = Strings["Settings.Hotkey.SavedNotGlobal"];
                break;
            default:
                input.State = HoykeyInputState.Warning;
                Status = Strings["Settings.Hotkey.HotkeyUnavailable"];
                break;
        }
    }



    [RelayCommand]
    private void OpenDesktopEntry()
    {
        var path = DesktopEntryPath;
        Status = path is null
            ? Strings["Settings.Hotkey.DesktopEntryMissing"]
            : SettingShellActions.TryOpen(path) is { } error
                ? string.Format(Strings["Settings.Common.OpenFailed"], error)
                : null;
    }


    /// <summary>桌面入口内容（只读展示，等价于 Starward 的"热键已注册"信息位）。</summary>
    public string DesktopEntryPreview => """
        [Desktop Entry]
        Type=Application
        Name=LunarWard
        Exec=lunarward %U
        MimeType=x-scheme-handler/lunarward;
        Categories=Game;Utility;
        """;
}
