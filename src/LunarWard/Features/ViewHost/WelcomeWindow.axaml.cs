using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using LunarWard.Features.Database;
using LunarWard.Frameworks;
using LunarWard.Helpers;

namespace LunarWard.Features.ViewHost;

/// <remarks>
/// 上游 <c>src/Starward/Features/ViewHost/WelcomeWindow.xaml(.cs)</c> 的移植。
/// 平台替换点（已登记 docs/09）：
/// <list type="bullet">
/// <item>WebView2 Runtime 检查 → WebKitGTK 版本（<c>Frameworks.WebView.WebViewAvailability.WebKitVersion</c>）；
/// 属性名沿用上游 <c>WebView2Version</c>（API 名不改，docs/10 §3），UI 文案显示 WebKitGTK。</item>
/// <item>WebP 解码器检查 → Avalonia/Skia 直接解码 WebP 样本字节。</item>
/// <item>智能应用控制提醒（Windows 11 专属）→ <c>IsWin11</c> 恒 false，区块隐藏但逻辑保留。</item>
/// <item>“透明/动画效果” <c>ms-settings:</c> 深链接 → 整块隐藏（Linux 桌面无此开关，属系统默认）。</item>
/// <item><c>ObservableObject</c> 属性通知 → Avalonia <c>StyledProperty</c>（Window 无法继承 ObservableObject，见 WindowEx 注释）。</item>
/// </list>
/// </remarks>
public partial class WelcomeWindow : WindowEx
{

    public static readonly StyledProperty<string?> UserDataFolderProperty =
        AvaloniaProperty.Register<WelcomeWindow, string?>(nameof(UserDataFolder));

    public static readonly StyledProperty<string?> UserDataFolderErrorMessageProperty =
        AvaloniaProperty.Register<WelcomeWindow, string?>(nameof(UserDataFolderErrorMessage));

    public static readonly StyledProperty<string?> WebView2VersionProperty =
        AvaloniaProperty.Register<WelcomeWindow, string?>(nameof(WebView2Version));

    public static readonly StyledProperty<bool> WebViewVersionMissingProperty =
        AvaloniaProperty.Register<WelcomeWindow, bool>(nameof(WebViewVersionMissing), true);

    public static readonly StyledProperty<bool> WebpDecoderSupportProperty =
        AvaloniaProperty.Register<WelcomeWindow, bool>(nameof(WebpDecoderSupport));

    public static readonly StyledProperty<bool> CanStartStarwardProperty =
        AvaloniaProperty.Register<WelcomeWindow, bool>(nameof(CanStartStarward));

    public static readonly StyledProperty<bool> IsWin11Property =
        AvaloniaProperty.Register<WelcomeWindow, bool>(nameof(IsWin11));


    public string? UserDataFolder
    {
        get => GetValue(UserDataFolderProperty);
        set => SetValue(UserDataFolderProperty, value);
    }

    public string? UserDataFolderErrorMessage
    {
        get => GetValue(UserDataFolderErrorMessageProperty);
        set => SetValue(UserDataFolderErrorMessageProperty, value);
    }

    public string? WebView2Version
    {
        get => GetValue(WebView2VersionProperty);
        set => SetValue(WebView2VersionProperty, value);
    }

    /// <summary>上游 XAML 用 <c>BindConverter.ObjectToVisibilityReversed(WebView2Version)</c>；Avalonia 绑定不支持函数调用，改为布尔属性。</summary>
    public bool WebViewVersionMissing
    {
        get => GetValue(WebViewVersionMissingProperty);
        set => SetValue(WebViewVersionMissingProperty, value);
    }

    public bool WebpDecoderSupport
    {
        get => GetValue(WebpDecoderSupportProperty);
        set => SetValue(WebpDecoderSupportProperty, value);
    }

    public bool CanStartStarward
    {
        get => GetValue(CanStartStarwardProperty);
        set => SetValue(CanStartStarwardProperty, value);
    }

    public bool IsWin11
    {
        get => GetValue(IsWin11Property);
        set => SetValue(IsWin11Property, value);
    }


    private readonly TaskCompletionSource<bool> _taskCompletionSource;


    public WelcomeWindow()
    {
        InitializeComponent();
        DataContext = this;
        _taskCompletionSource = new();
        Closed += WelcomeWindow_Closed;
        CenterInScreen(1200, 676);
        SystemDecorations = SystemDecorations.Full;
        CanResize = true;
        CanMinimize = false;
        CanMaximize = false;
    }


    private void WelcomeWindow_Closed(object? sender, EventArgs e)
    {
        _taskCompletionSource.TrySetResult(false);
    }


    public async Task<bool> WaitAsync()
    {
        Activate();
        return await _taskCompletionSource.Task;
    }


    private async void Grid_Loaded(object? sender, RoutedEventArgs e)
    {
        // Linux 无 Windows 11 / 智能应用控制
        IsWin11 = false;
        InitializeDefaultUserDataFolder();
        CheckWritePermission();
        CheckWebView2Support();
        await CheckWebpDecoderSupportAsync();
    }


    private void InitializeDefaultUserDataFolder()
    {
        try
        {
            string? parentFolder = new DirectoryInfo(AppContext.BaseDirectory).Parent?.FullName;
            if (AppConfig.IsPortable)
            {
                UserDataFolder = parentFolder;
            }
            else if (AppConfig.IsAppInRemovableStorage)
            {
                // 上游 ".StarwardData"
                UserDataFolder = Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory)!, ".LunarWardData");
            }
            else
            {
#if DEBUG
                UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunarWard");
#else
                UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LunarWard");
#endif
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    private void CheckWritePermission()
    {
        try
        {
            UserDataFolderErrorMessage = null;
            CanStartStarward = false;
            if (string.IsNullOrWhiteSpace(UserDataFolder) || !Path.IsPathFullyQualified(UserDataFolder))
            {
                UserDataFolderErrorMessage = Lang.DownloadGamePage_TheFolderDoesNotExist;
                return;
            }
            string folder = Path.GetFullPath(UserDataFolder);
            Directory.CreateDirectory(folder);
            if (folder == Path.GetPathRoot(folder))
            {
                UserDataFolderErrorMessage = Lang.LauncherPage_PleaseDoNotSelectTheRootDirectoryOfADrive;
                return;
            }
            string baseDir = AppContext.BaseDirectory.TrimEnd('/', '\\');
            if (folder.StartsWith(baseDir))
            {
                UserDataFolderErrorMessage = Lang.SelectDirectoryPage_AutoDeleteAfterUpdate;
                return;
            }
            var file = Path.Combine(folder, Guid.CreateVersion7().ToString());
            File.WriteAllBytes(file, "Write permission test."u8);
            File.Delete(file);
            CanStartStarward = true;
        }
        catch (UnauthorizedAccessException ex)
        {
            UserDataFolderErrorMessage = Lang.SelectDirectoryPage_NoWritePermission;
            Debug.WriteLine(ex);
        }
        catch (Exception ex)
        {
            UserDataFolderErrorMessage = ex.Message;
            Debug.WriteLine(ex);
        }
    }


    private async void ChangeUserDataFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? folder = await FileDialogHelper.PickFolderAsync(TopLevel.GetTopLevel(this));
            if (Directory.Exists(folder))
            {
                UserDataFolder = folder;
                CheckWritePermission();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    private void CheckWebView2Support()
    {
        try
        {
            // 上游 CoreWebView2Environment.GetAvailableBrowserVersionString() → WebKitGTK 版本
            WebView2Version = Frameworks.WebView.WebViewAvailability.WebKitVersion;
            WebViewVersionMissing = string.IsNullOrWhiteSpace(WebView2Version);
        }
        catch (Exception ex)
        {
            WebViewVersionMissing = true;
            Debug.WriteLine(ex);
        }
    }


    private async Task CheckWebpDecoderSupportAsync()
    {
        try
        {
            byte[] bytes = Convert.FromBase64String("UklGRiQAAABXRUJQVlA4IBgAAAAwAQCdASoBAAEAAgA0JaQAA3AA/vv9UAA=");
            WebpDecoderSupport = await Task.Run(() =>
            {
                using MemoryStream ms = new(bytes);
                using var bitmap = new Bitmap(ms);
                return bitmap.PixelSize.Width > 0;
            });
        }
        catch (Exception ex)
        {
            // 上游同样吞异常：解码器缺失时保持 NotInstalled
            Debug.WriteLine(ex);
        }
    }


    private void InstallWebViewButton_Click(object? sender, RoutedEventArgs e)
    {
        // Windows 上装 WebView2 Runtime；Linux 上对应 WebKitGTK 系统库，给出官方安装说明
        TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri("https://webkitgtk.org/"));
    }


    private void InstallWebpButton_Click(object? sender, RoutedEventArgs e)
    {
        // 上游跳 Microsoft Store 装 WebP 扩展；Linux 上 Skia 自带 WebP，缺失即发行版构建问题
        TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri("https://github.com/Scighost/Starward"));
    }


    private void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!Directory.Exists(UserDataFolder))
            {
                UserDataFolderErrorMessage = Lang.DownloadGamePage_TheFolderDoesNotExist;
                CanStartStarward = false;
                return;
            }
            AppConfig.UserDataFolder = UserDataFolder;
            DatabaseService.SetDatabase(UserDataFolder!);
            AppConfig.SaveConfiguration();
            _taskCompletionSource.TrySetResult(true);
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Start LunarWard from welcome window");
            Debug.WriteLine(ex);
        }
    }

}
