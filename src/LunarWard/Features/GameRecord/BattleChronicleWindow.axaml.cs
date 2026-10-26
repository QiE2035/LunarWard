using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using LunarWard.Core.GameRecord;
using LunarWard.Frameworks;


namespace LunarWard.Features.GameRecord;
public sealed partial class BattleChronicleWindow : WindowEx
{


    public BattleChronicleWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
    }


    private void InitializeWindow()
    {
        // Linux: 上游 AppWindow.TitleBar.ExtendsContentIntoTitleBar = true；
        // Avalonia 等价物是把客户区扩展到标题栏（标题栏由本窗口的 XAML 自绘）。
        ExtendClientAreaToDecorationsHint = true;
        Title = Lang.HoyolabToolboxPage_BattleChronicle;
        // Linux: 上游 RootGrid.RequestedTheme = Dark/Light；Avalonia 用 ThemeVariant。
        RequestedThemeVariant = ShouldAppsUseDarkMode() ? ThemeVariant.Dark : ThemeVariant.Light;
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
        CenterInScreen();
    }


    private void CenterInScreen()
    {
        // Linux: 上游用 DisplayArea + AppWindow.MoveAndResize（Win32 显示区）；
        // Avalonia 等价物是 Screens.WorkingArea + WindowEx.CenterInScreen(width, height)。
        Screen? screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }
        double h = screen.WorkingArea.Height * 0.95;
        double w = h / 16.0 * 9.0;
        if (w > screen.WorkingArea.Width)
        {
            w = screen.WorkingArea.Width * 0.95;
            h = w * 16.0 / 9.0;
        }
        CenterInScreen((int)w, (int)h);
    }


    private GameRecordRole? _currentRole;

    /// <summary>
    /// 上游用 <c>x:Bind</c> 把 <see cref="BBSWebBridge.CurrentGameBiz"/> / <c>GameRecordRole</c> 直接绑到这里；
    /// Avalonia 的反射绑定无法把 <c>object</c> 赋给强类型 DirectProperty，故改为在 setter 里显式下推。
    /// </summary>
    public GameRecordRole? CurrentRole
    {
        get => _currentRole;
        set
        {
            if (SetProperty(ref _currentRole, value) && value is { } role)
            {
                bbsWebBridge.CurrentGameBiz = role.GameBiz;
                bbsWebBridge.GameRecordRole = role;
            }
        }
    }


    private void RootGrid_Loaded(object? sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= RootGrid_Loaded;
        _ = bbsWebBridge.LoadPageAsync();
    }


}
