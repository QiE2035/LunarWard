using Avalonia;
using Avalonia.Controls;
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
        RootGrid.RequestedThemeVariant = ShouldAppsUseDarkMode() ? ThemeVariant.Dark : ThemeVariant.Light;
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


    public GameRecordRole? CurrentRole { get; set => SetProperty(ref field, value); }


    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= RootGrid_Loaded;
        _ = bbsWebBridge.LoadPageAsync();
    }


}
