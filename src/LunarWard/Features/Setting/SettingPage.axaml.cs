using System.ComponentModel;
using Avalonia.LogicalTree;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Controls;
using Avalonia.Threading;
using LunarWard.Language;
using LunarWard.Services;
using LunarWard.Frameworks;

namespace LunarWard.Features.Setting;

/// <summary>
/// 设置页（Starward <c>Features/Setting/SettingPage.xaml</c>）：260px 二级导航 + 右侧子页宿主。
/// </summary>
/// <remarks>
/// <para>
/// 子页用 <c>ContentControl</c> + 字典缓存实例（Starward 的 <c>Frame_Setting</c> 也是这样：
/// 同一次设置会话里切回某个子页时状态保留）。
/// </para>
/// <para>
/// 外壳把页面实例缓存在 <c>StarwardShell</c> 里，离开设置页时会调
/// <see cref="StarwardPage.DisposeViewModel"/> 释放子页；再次进入时同一个实例被复用，
/// 所以 <see cref="OnNavigatedTo"/> 必须能重建子页，否则会拿到已释放的对象。
/// </para>
/// <para>
/// 与上游一样，成员直接落在 code-behind：二次确认的开关/文案/命令就是本页属性
/// （上游是页面里的 <c>ContentDialog</c>），不再经过 <c>SettingPageViewModel</c>。
/// </para>
/// </remarks>
public partial class SettingPage : PageBase
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
    private readonly Dictionary<SettingSectionKind, PageBase> _sections = [];

    private SettingSectionKind _currentSection;

    private bool _syncingSelection;


    public SettingPage()
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();
        DataContext = this;

        // 语言切换：设置页已迁到上游 Lang 资源，静态绑定不会自更新，宿主负责重建文案与子页。
        WeakReferenceMessenger.Default.UnregisterAll(this);
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (_, _) => OnLanguageChanged());

        _currentSection = ResolveInitialSection();
        SelectSection(_currentSection);
    }


    /// <summary>
    /// 目录/自检根：从别页回来时重建右侧子页（等价上游重新 <c>Navigate</c> 到默认页）。
    /// </summary>
    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        if (SectionHost.Content is null)
        {
            SelectSection(_currentSection);
        }

        // TEMP 清理缓存自检（等价点击「清理缓存」→「确定」）
        if (Environment.GetEnvironmentVariable("LUNARWARD_CLEAR_CACHE") == "1")
        {
            ShowSection(SettingSectionKind.FileManage);
            DispatcherTimer.RunOnce(async () =>
            {
                if (_sections.TryGetValue(SettingSectionKind.FileManage, out var section)
                    && section is FileManageSetting files)
                {
                    await files.ClearCacheCommand.ExecuteAsync(null);
                }
            }, TimeSpan.FromMilliseconds(300));
            DispatcherTimer.RunOnce(() => ConfirmCommand.Execute(null), TimeSpan.FromMilliseconds(900));
        }
    }


    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        _sections.Clear();
        SectionHost.Content = null;
    }


    /// <summary>
    /// 自检入口：按 Starward 页面名切换到指定二级子页。
    /// </summary>
    /// <remarks>
    /// <c>--page settings --settings-section files</c> 用；tag 既接受 Starward 短名
    /// （<c>about</c> / <c>files</c> …），也接受 XAML 里的完整 Tag（<c>FileManageSetting</c>）。
    /// 与 Starward 的 <c>NavigationView_ItemInvoked</c> 一样只做"选中 + 换 Frame 内容"。
    /// </remarks>
    public void SelectSection(string tag)
    {
        if (SettingSectionTags.Parse(tag) is { } kind)
        {
            SelectSection(kind);
        }
    }


    /// <summary>切换二级子页（同步左侧选中态，首次访问时创建并缓存）。</summary>
    private void SelectSection(SettingSectionKind kind)
    {
        _currentSection = kind;

        var item = SectionList.Items.OfType<ListBoxItem>()
            .FirstOrDefault(x => SettingSectionTags.Parse(x.Tag as string) == kind);
        if (item is not null && !ReferenceEquals(SectionList.SelectedItem, item))
        {
            _syncingSelection = true;
            try
            {
                SectionList.SelectedItem = item;
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        ShowSection(kind);
    }


    /// <summary>界面语言变化后刷新本页静态文案，并重建当前子页（上游是 <c>Bindings.Update()</c>）。</summary>
    private void OnLanguageChanged()
    {
        var header = FindTextBlocks(this).FirstOrDefault(x => x.Classes.Contains("pane-header"));
        if (header is not null)
        {
            header.Text = Lang.SettingPage_AppSettings;
        }

        foreach (var item in SectionList.Items.OfType<ListBoxItem>())
        {
            if (SettingSectionTags.Parse(item.Tag as string) is not { } kind)
            {
                continue;
            }

            if (FindTextBlocks(item).LastOrDefault() is { } label)
            {
                label.Text = kind switch
                {
                    SettingSectionKind.About => Lang.SettingPage_About,
                    SettingSectionKind.General => Lang.SettingPage_General,
                    SettingSectionKind.Download => Lang.SettingPage_Download,
                    SettingSectionKind.FileManage => Lang.SettingPage_FileManagement,
                    SettingSectionKind.Screenshot => Lang.MainPage_GameScreenshot,
                    SettingSectionKind.Hotkey => Lang.SettingPage_KeyboardShortcuts,
                    SettingSectionKind.Gamepad => Lang.SettingPage_ControllerEnhancement,
                    SettingSectionKind.Advanced => Lang.SettingPage_Advanced,
                    SettingSectionKind.Toolbox => Lang.SettingPage_Toolbox,
                    _ => label.Text,
                };
            }
        }

        // 子页的文案是编译期静态绑定，只能重建实例；旧实例随字典丢弃。
        _sections.Clear();
        ShowSection(_currentSection);
    }


    private static IEnumerable<TextBlock> FindTextBlocks(ILogical logical)
    {
        foreach (var child in logical.LogicalChildren)
        {
            if (child is TextBlock textBlock)
            {
                yield return textBlock;
            }

            foreach (var descendant in FindTextBlocks(child))
            {
                yield return descendant;
            }
        }
    }


    private void ShowSection(SettingSectionKind kind)
    {
        if (!_sections.TryGetValue(kind, out var section))
        {
            section = CreateSection(kind);
            _sections[kind] = section;
        }

        SectionHost.Content = section;
    }


    /// <summary>子页工厂：与上游 <c>NavigationView_ItemInvoked</c> 的 switch 一一对应。</summary>
    private PageBase CreateSection(SettingSectionKind kind) => kind switch
    {
        SettingSectionKind.About => new AboutSetting(),
        SettingSectionKind.General => new GeneralSetting(),
        SettingSectionKind.Download => new DownloadSetting(),
        SettingSectionKind.FileManage => new FileManageSetting(ConfirmAsync),
        SettingSectionKind.Screenshot => new ScreenshotSetting(),
        SettingSectionKind.Hotkey => new HotkeySetting(),
        SettingSectionKind.Gamepad => new GamepadControlSetting(),
        SettingSectionKind.Advanced => new AdvancedSetting(),
        SettingSectionKind.Toolbox => new ToolboxSetting(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };


    private void OnSectionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
        {
            return;
        }

        if (SectionList.SelectedItem is ListBoxItem item
            && SettingSectionTags.Parse(item.Tag as string) is { } kind)
        {
            SelectSection(kind);
        }
    }


    /// <summary>
    /// 初始子页。默认「关于」（与 Starward <c>Frame_Setting.Navigate(typeof(AboutSetting))</c> 一致）；
    /// 自检/截图时可用 <c>--settings-section &lt;tag&gt;</c>（旧开关
    /// <c>LUNARWARD_SETTINGS_SECTION</c> 仍保留）直接落到某一页。
    /// </summary>
    private static SettingSectionKind ResolveInitialSection()
    {
        var requested = CaptureMode.SettingsSection;
        if (string.IsNullOrWhiteSpace(requested))
        {
            requested = Environment.GetEnvironmentVariable("LUNARWARD_SETTINGS_SECTION");
        }

        return SettingSectionTags.Parse(requested) ?? SettingSectionKind.About;
    }


    // ── 页内二次确认（上游 FileManageSetting 的 ContentDialog）──────────

    private TaskCompletionSource<bool>? _confirmCompletion;


    /// <summary>确认框是否显示。</summary>
    public bool IsConfirmOpen
    {
        get => _isConfirmOpen;
        private set => SetProperty(ref _isConfirmOpen, value);
    }

    private bool _isConfirmOpen;

    /// <summary>确认框标题。</summary>
    public string ConfirmTitle
    {
        get => _confirmTitle;
        private set => SetProperty(ref _confirmTitle, value);
    }

    private string _confirmTitle = "";

    /// <summary>确认框正文。</summary>
    public string ConfirmBody
    {
        get => _confirmBody;
        private set => SetProperty(ref _confirmBody, value);
    }

    private string _confirmBody = "";

    /// <summary>确认按钮文案。</summary>
    public string ConfirmText
    {
        get => _confirmText;
        private set => SetProperty(ref _confirmText, value);
    }

    private string _confirmText = "";

    /// <summary>取消按钮文案。</summary>
    public string CancelText
    {
        get => _cancelText;
        private set => SetProperty(ref _cancelText, value);
    }

    private string _cancelText = "";


    /// <summary>显示确认框，等待用户点「确定」/「取消」。</summary>
    public Task<bool> ConfirmAsync(string title, string body)
    {
        // 连续两次请求时，把前一个请求当成取消（避免悬挂的 Task）
        _confirmCompletion?.TrySetResult(false);

        _confirmCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ConfirmTitle = title;
        ConfirmBody = body;
        ConfirmText = Lang.SelectDirectoryPage_OK;
        CancelText = Lang.SelectDirectoryPage_Cancel;
        IsConfirmOpen = true;
        return _confirmCompletion.Task;
    }


    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Confirm() => CompleteConfirm(true);


    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Cancel() => CompleteConfirm(false);


    private void CompleteConfirm(bool result)
    {
        IsConfirmOpen = false;
        var completion = _confirmCompletion;
        _confirmCompletion = null;
        completion?.TrySetResult(result);
    }
}


/// <summary>设置页二级导航的页面种类（Starward <c>NavigationViewItem.Tag</c> 的强类型版）。</summary>
public enum SettingSectionKind
{
    About,
    General,
    Download,
    FileManage,
    Screenshot,
    Hotkey,
    Gamepad,
    Advanced,
    Toolbox,
}


/// <summary>把 <c>--settings-section</c> / <c>ListBoxItem.Tag</c> 解析成 <see cref="SettingSectionKind"/>。</summary>
internal static class SettingSectionTags
{
    public static SettingSectionKind? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var text = tag.Trim().Replace("-", "").Replace("_", "");
        if (text.EndsWith("Setting", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^"Setting".Length];
        }

        return text.ToLowerInvariant() switch
        {
            "about" => SettingSectionKind.About,
            "general" => SettingSectionKind.General,
            "download" => SettingSectionKind.Download,
            "files" or "filemanage" or "filemanagement" => SettingSectionKind.FileManage,
            "screenshot" or "screenshots" => SettingSectionKind.Screenshot,
            "hotkey" or "hotkeys" or "keyboardshortcuts" => SettingSectionKind.Hotkey,
            "gamepad" or "gamepadcontrol" or "controllerenhancement" => SettingSectionKind.Gamepad,
            "advanced" => SettingSectionKind.Advanced,
            "toolbox" => SettingSectionKind.Toolbox,
            _ => null,
        };
    }
}
