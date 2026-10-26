using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Features.Background;
using LunarWard.Features.Database;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.Overlay;
using LunarWard.Features.Screenshot;
using LunarWard.Frameworks;
using LunarWard.Language;
using LunarWard.Services;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace LunarWard.Features.ViewHost;

/// <summary>
/// 上游 <c>Starward.Features.ViewHost.MainWindow</c> 的 1:1 移植。
/// </summary>
/// <remarks>
/// <b>平台替换</b>（docs/09 §五）：
/// <list type="bullet">
/// <item><c>AppWindow.TitleBar.*</c> / <c>OverlappedPresenter</c> → <see cref="WindowEx"/> 已封装的窗口装饰与
/// <c>CanResize=false</c>；</item>
/// <item><c>WindowSubclassProc</c>（WM_HOTKEY / WM_WTSSESSION_CHANGE / WM_DEVICECHANGE）→ X11 下没有等价的
/// 窗口子类化钩子：全局热键由 <c>HotkeyManager</c> 在 Linux 返回不支持，锁屏/设备插拔改由
/// <see cref="LunarWard.Features.Database.DatabaseService"/> 与托盘逻辑各自处理，本类只保留消息发送入口；</item>
/// <item><c>AppWindow.Closing</c> → <c>Window.Closing</c>；<c>ContentDialog</c> → <see cref="DialogService"/> +
/// <see cref="MainWindowCloseDialog"/>；</item>
/// <item><c>App.Current.Exit()</c> → <see cref="Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime.Shutdown"/>。</item>
/// </list>
/// </remarks>
public sealed partial class MainWindow : WindowEx, INotifyPropertyChanged
{

    public static new MainWindow? Current { get; private set; }


    public event PropertyChangedEventHandler? PropertyChanged;


    public MainWindow()
    {
        Current = this;
        InitializeComponent();
        InitializeMainWindow();
        App.EnsureSystemTray();
        WeakReferenceMessenger.Default.Register<AccentColorChangedMessage>(this, OnAccentColorChanged);
        WeakReferenceMessenger.Default.Register<GameStartedMessage>(this, OnGameStarted);
    }


    private void InitializeMainWindow()
    {
        Title = "LunarWard";
        CanResize = false;
        CenterInScreen(1200, 676);
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
        Closing += MainWindow_Closing;
        KeyDown += Content_KeyDown;
        // 上游 SetDragRectangles(new RectInt32(0, 0, 100000, 48 * UIScale))：X11 无 drag rectangle，
        // 由 WindowEx.SetDragRectangles 记录（Linux 下移动窗口靠标题栏/快捷键），见 docs/09 §五。
        SetDragRectangles(new PixelRect(0, 0, 100000, (int)(48 * UIScale)));
    }


    public override void Show()
    {
        double uiScale = UIScale;
        if (Math.Abs(Bounds.Width - 1200 * uiScale) > 10 || Math.Abs(Bounds.Height - 676 * uiScale) > 10)
        {
            CenterInScreen(1200, 676);
        }
        base.Show();
    }


    public void ShowByGamepad()
    {
        CenterInScreen(1200, 676);
        base.Show();
        Activate();
    }


    private void OnGameStarted(object? recipient, GameStartedMessage message)
    {
        StartGameAction action = AppConfig.StartGameAction;
        if (action is StartGameAction.Hide)
        {
            Hide();
        }
        else if (action is StartGameAction.Minimize)
        {
            WindowState = WindowState.Minimized;
        }
    }


    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            e.Cancel = true;
            MainWindowCloseOption option = AppConfig.CloseWindowOption;
            if (option is not MainWindowCloseOption.Hide and not MainWindowCloseOption.Exit)
            {
                var dialog = new MainWindowCloseDialog
                {
                    Title = Lang.ExperienceSettingPage_CloseWindowOption,
                    PrimaryButtonText = Lang.Common_Confirm,
                    CloseButtonText = Lang.Common_Cancel,
                };
                bool confirmed = false;
                dialog.PrimaryButtonClicked += (_, _) => confirmed = true;
                await DialogService.ShowAsync(dialog, Content as Control);
                if (!confirmed)
                {
                    return;
                }
                option = dialog.MainWindowCloseOption.Value;
                AppConfig.CloseWindowOption = option;
            }
            if (option is MainWindowCloseOption.Hide)
            {
                Hide();
            }
            if (option is MainWindowCloseOption.Exit)
            {
                Close();
                Task backupTask = Task.Run(DatabaseService.AutoBackupToAppDataLocal);
                Task timeTask = Task.Delay(30000);
                await Task.WhenAny(backupTask, timeTask);
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
        }
        catch { }
    }


    private void OnAccentColorChanged(object? recipient, AccentColorChangedMessage message)
    {
        // 上游通过切换 RequestedTheme 强制 WinUI 重新解析主题资源以刷新强调色。
        // Avalonia 里强调色由 Styles 资源直接引用，重新 Apply 一次样式的轻量等价做法是让主题变体抖动一次。
        if (Content is Control content)
        {
            var variant = content.ActualThemeVariant;
            Application.Current!.RequestedThemeVariant = variant == Avalonia.Styling.ThemeVariant.Dark
                ? Avalonia.Styling.ThemeVariant.Light
                : Avalonia.Styling.ThemeVariant.Dark;
            Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default;
        }
    }


    private void Content_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            Hide();
        }
    }


    public override void Hide()
    {
        base.Hide();
        WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage { Hide = true, CurrentTime = DateTimeOffset.Now });
        GC.Collect();
    }

}
