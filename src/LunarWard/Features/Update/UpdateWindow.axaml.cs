using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Features.Setting;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using LunarWard.Language;
using LunarWard.Setup.Core;
using LunarWard.Setup.Core.Github;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.Update;

/// <summary>
/// 更新窗口。<b>移植自</b> <c>Starward/src/Starward/Features/Update/UpdateWindow.xaml(.cs)</c>。
/// </summary>
/// <remarks>
/// 平台替换（逐条登记于 <c>docs/09-LINUX-DIVERGENCES.md</c>）：
/// <list type="bullet">
/// <item><c>[INotifyPropertyChanged]</c>：Avalonia 的 <see cref="Window"/> 已实现 INPC 语义，Toolkit 源生成器无法叠加，
/// 与 <c>UIGF4GachaWindow</c> 一致地手写 <c>SetProperty/OnPropertyChanged</c>。</item>
/// <item><c>DesktopAcrylicBackdrop</c> → 窗口背景 <c>StarAcrylicBrush</c>（无合成器模糊，只有半透明底色）。</item>
/// <item><c>AppWindow.MoveAndResize</c> + <c>DisplayArea</c> → <see cref="WindowEx.CenterInScreen(int?, int?)"/>（按屏幕工作区等比收缩）。</item>
/// <item><c>DispatcherQueueTimer</c> → <see cref="DispatcherTimer"/>。</item>
/// <item><c>Windows.System.Launcher</c> → <see cref="Frameworks.ShellHelper.OpenUri"/>（xdg-open 语义）。</item>
/// <item>WebView2 初始化失败的 <c>COMException</c> → <see cref="InvalidOperationException"/>（WebKitGTK 缺失时抛出）。</item>
/// <item><c>Finish()</c> 里的 <c>RpcService.KeepRunningOnExited(false, noLongerChange: true)</c>：本仓库 Features/RPC 尚未移植，
/// 留 <c>TODO(port)</c>，不改变其余流程。</item>
/// <item><c>Win32Exception(1223)</c>（UAC 取消）在 Linux 不存在对应错误码，删除该 catch 分支。</item>
/// </list>
/// </remarks>
public sealed partial class UpdateWindow : WindowEx, INotifyPropertyChanged
{


    private readonly ILogger<UpdateWindow> _logger = AppConfig.GetLogger<UpdateWindow>();

    private readonly ReleaseClient _releaseClient = AppConfig.GetService<ReleaseClient>();

    private readonly UpdateService _updateService = AppConfig.GetService<UpdateService>();

    private readonly SetupService _setupService = AppConfig.GetService<SetupService>();


    private readonly DispatcherTimer _timer;



    public UpdateWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += _timer_Tick;
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (_, _) => RefreshBindings());
        this.Closed += UpdateWindow_Closed;
        DataContext = this;
    }



    private void InitializeWindow()
    {
        Title = "LunarWard · Update";
        CanResize = false;
        // 上游给 RootGrid 设 RequestedTheme（Grid 支持），Avalonia 的 RequestedThemeVariant 只在 StyledElement 上，
        // 这里提到窗口级，语义等价（整个内容区都跟随）。
        RequestedThemeVariant = ShouldAppsUseDarkMode() ? ThemeVariant.Dark : ThemeVariant.Light;
        // 上游 DesktopAcrylicBackdrop → 半透明底色（docs/09）：星穹背景色 + 全透明窗口
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = this.TryFindResource("StarAcrylicBrush", out object? brush) ? brush as Avalonia.Media.IBrush : null;
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
    }



    /// <summary>
    /// 上游用 <c>DisplayArea.GetFromWindowId(...).WorkArea</c> + <c>AppWindow.MoveAndResize</c> 手工居中缩放；
    /// Avalonia 侧按同一公式算逻辑尺寸后交给 <see cref="WindowEx.CenterInScreen(int?, int?)"/>。
    /// </summary>
    private void CenterInScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }
        var workArea = screen.WorkingArea;
        double scale = screen.Scaling;
        int workWidth = (int)(workArea.Width / scale);
        int workHeight = (int)(workArea.Height / scale);
        int w, h;
        if (NewVersion is null)
        {
            Grid_Update.IsVisible = false;
            h = (int)(workHeight * 0.95);
            w = (int)(h / 4.0 * 3.0);
            if (w > workWidth)
            {
                w = (int)(workWidth * 0.95);
                h = (int)(w * 4.0 / 3.0);
            }
        }
        else
        {
            Button_RemindLatter.IsVisible = false;
            w = 1000;
            h = (int)(w / 4.0 * 3.0);
            if (w > workWidth || h > workHeight)
            {
                h = (int)(workHeight * 0.9);
                w = (int)(h / 4.0 * 3.0);
                if (w > workWidth)
                {
                    w = (int)(workWidth * 0.9);
                    h = (int)(w * 4.0 / 3.0);
                }
            }
        }
        CenterInScreen(w, h);
    }



    public new void Activate()
    {
        CenterInScreen();
        base.Activate();
    }



    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (NewVersion?.DisableAutoUpdate ?? false)
        {
            IsUpdateNowEnabled = false;
            ErrorMessage = Lang.UpdatePage_YouNeedToManuallyDownloadTheNewVersionPackage;
        }
        if (UpdateService.UpdateFinished)
        {
            Finish(skipRestart: true);
        }
        _ = LoadUpdateContentAsync();
    }



    private void UpdateWindow_Closed(object? sender, EventArgs args)
    {
        _timer.Stop();
        _timer.Tick -= _timer_Tick;
        _updateService.StopUpdate();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        this.Closed -= UpdateWindow_Closed;
    }




    public ReleaseInfoDetail? NewVersion
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ArchitectureText));
                OnPropertyChanged(nameof(PackageSizeText));
            }
        }
    }


    /// <summary>
    /// 上游 XAML 用三个 <c>Run</c>（<c>Architecture</c> + "-" + <c>InstallType</c>）拼接；
    /// Avalonia 的 <c>Run</c> 绑定同样可用，这里为了少一次文案切换直接给组合串（文案与上游一致）。
    /// </summary>
    public string? ArchitectureText => NewVersion is null ? null : $"{NewVersion.Architecture} - {NewVersion.InstallType}";


    /// <summary>上游 XAML：<c>local:UpdateWindow.ByteLengthToString(NewVersion.PackageSize)</c>；Avalonia 不能绑定静态方法，改为属性。</summary>
    public string? PackageSizeText => NewVersion is null ? null : ByteLengthToString(NewVersion.PackageSize);


#if DEBUG
    public string ChannelText => Lang.UpdatePage_DevChannel;
#else
    public string ChannelText => AppConfig.EnablePreviewRelease ? Lang.UpdatePage_PreviewChannel : Lang.UpdatePage_StableChannel;
#endif



    private async void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Control fe && NewVersion != null)
            {
                var url = fe.Tag switch
                {
                    "release" => $"https://github.com/QiE2035/LunarWard/releases/tag/{NewVersion.Version}",
                    "package" => NewVersion.PackageUrl,
                    _ => null,
                };
                _logger.LogInformation("Open url: {url}", url);
                if (url is not null)
                {
                    Frameworks.ShellHelper.OpenUri(url);
                }
            }
        }
        catch { }
    }





    #region Update



    public bool IsUpdateNowEnabled { get; set => SetProperty(ref field, value); } = true;

    public bool IsUpdateRemindLatterEnabled { get; set => SetProperty(ref field, value); } = true;

    public bool IsProgressTextVisible { get; set => SetProperty(ref field, value); }

    public bool IsProgressBarVisible { get; set => SetProperty(ref field, value); }

    public string ProgressBytesText { get; set => SetProperty(ref field, value); }

    public string ProgressPercentText { get; set => SetProperty(ref field, value); }

    public string? ErrorMessage { get; set => SetProperty(ref field, value); }



    public bool AutoRestartWhenUpdateFinished
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.AutoRestartWhenUpdateFinished = value;
            }
        }
    } = AppConfig.AutoRestartWhenUpdateFinished;



    public bool ShowUpdateContentAfterUpdateRestart
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.ShowUpdateContentAfterUpdateRestart = value;
            }
        }
    } = AppConfig.ShowUpdateContentAfterUpdateRestart;



    private CancellationTokenSource? _updateCts;


    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        try
        {
            ErrorMessage = null;
            IsUpdateNowEnabled = false;
            IsUpdateRemindLatterEnabled = false;


            if (NewVersion != null)
            {
                if (AppConfig.InstallType is InstallType.Setup && NewVersion.Setup is not null)
                {
                    Button_Restart.IsEnabled = false;
                    _updateCts?.Cancel();
                    _updateCts = new CancellationTokenSource();
                    CancellationToken cancellationToken = _updateCts.Token;
                    IsProgressTextVisible = true;
                    IsProgressBarVisible = true;

                    var task = _setupService.UpdateAsync(NewVersion, cancellationToken);

                    const double MB = 1 << 20;
                    while (!task.IsCompleted)
                    {
                        if (_setupService.SetupTotalBytes > 0)
                        {
                            TextBlock_Bytes.Text = $"{_setupService.SetupDownloadBytes / MB:F2}/{_setupService.SetupTotalBytes / MB:F2} MB";
                            ProgressBar_Update.Value = _setupService.SetupDownloadBytes * 100.0 / _setupService.SetupTotalBytes;
                        }
                        await Task.Delay(100);
                    }
                    await task;
                    Button_Restart.IsEnabled = true;
                    IsProgressTextVisible = false;
                    IsProgressBarVisible = false;
                }
                if (AppConfig.IsPortable)
                {
                    _timer.Start();
                    await _updateService.StartUpdateAsync(NewVersion);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Button_UpdateNow.IsEnabled = true;
            Button_RemindLatter.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update now");
            Button_UpdateNow.IsEnabled = true;
            Button_RemindLatter.IsEnabled = true;
        }
    }




    private void UpdateProgressState()
    {
        if (_updateService.State is UpdateState.Pending)
        {
            IsProgressTextVisible = true;
            IsProgressBarVisible = true;
            ProgressBar_Update.IsIndeterminate = true;
            UpdateProgressValue();
        }
        else if (_updateService.State is UpdateState.Downloading)
        {
            IsUpdateNowEnabled = false;
            IsUpdateRemindLatterEnabled = false;
            IsProgressBarVisible = true;
            IsProgressTextVisible = true;
            ProgressBar_Update.IsIndeterminate = false;
            UpdateProgressValue();
        }
        else if (_updateService.State is UpdateState.Finish)
        {
            IsProgressTextVisible = false;
            ProgressBar_Update.IsIndeterminate = false;
            ProgressBar_Update.Value = 100;
        }
        else if (_updateService.State is UpdateState.Stop)
        {
            IsUpdateNowEnabled = true;
            IsUpdateRemindLatterEnabled = true;
            IsProgressTextVisible = false;
            IsProgressBarVisible = false;
            ErrorMessage = null;
        }
        else if (_updateService.State is UpdateState.Error)
        {
            IsUpdateNowEnabled = true;
            IsUpdateRemindLatterEnabled = true;
            IsProgressTextVisible = false;
            IsProgressBarVisible = false;
            ErrorMessage = _updateService.ErrorMessage;
        }
        else if (_updateService.State is UpdateState.NotSupport)
        {
            IsUpdateNowEnabled = false;
            IsUpdateRemindLatterEnabled = true;
            IsProgressTextVisible = false;
            IsProgressBarVisible = false;
            ErrorMessage = _updateService.ErrorMessage;
        }
    }



    private void UpdateProgressValue()
    {
        if (_updateService.Progress_TotalBytes == 0 || _updateService.Progress_DownloadBytes == 0)
        {
            ProgressBytesText = "";
            return;
        }
        const double mb = 1 << 20;
        ProgressBytesText = $"{_updateService.Progress_DownloadBytes / mb:F2}/{_updateService.Progress_TotalBytes / mb:F2} MB";
        var progress = (double)_updateService.Progress_DownloadBytes / _updateService.Progress_TotalBytes;
        ProgressPercentText = $"{progress:P1}";
        ProgressBar_Update.Value = progress * 100;
    }




    private void _timer_Tick(object? sender, EventArgs args)
    {

        try
        {
            UpdateProgressState();
            if (_updateService.State is UpdateState.Finish)
            {
                _timer.Stop();
                Finish();
            }
            else if (_updateService.State is UpdateState.Stop or UpdateState.Error or UpdateState.NotSupport)
            {
                _timer.Stop();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update progress");
        }
    }



    private void Finish(bool skipRestart = false)
    {
        AppConfig.IgnoreVersion = null;
        Button_UpdateNow.IsVisible = false;
        Button_Restart.IsVisible = true;
        // TODO(port): 上游此处调用 RpcService.KeepRunningOnExited(false, noLongerChange: true)，
        // 让 Discord RPC 在本次退出后不再重启；本仓库 Features/RPC 尚未移植（docs/09）。
        if (AutoRestartWhenUpdateFinished && !skipRestart)
        {
            Restart();
        }
    }



    [RelayCommand]
    private void Restart()
    {
        try
        {
            string? launcher = AppConfig.StarwardPortableLauncherExecutePath;
            if (launcher is not null && File.Exists(launcher))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = launcher,
                    WorkingDirectory = Path.GetDirectoryName(launcher),
                });
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Restart");
            ErrorMessage = ex.Message;
        }
    }



    [RelayCommand]
    private void RemindMeLatter()
    {
        this.Close();
    }



    [RelayCommand]
    private void IgnoreThisVersion()
    {
        if (NewVersion is null)
        {
            AppConfig.LastAppVersion = AppConfig.AppVersion;
        }
        else
        {
            AppConfig.IgnoreVersion = NewVersion.Version;
        }
        this.Close();
    }




    private void Button_UpdateRemindLatter_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        Button_UpdateRemindLatter.Opacity = 1;
    }


    private void Button_UpdateRemindLatter_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        Button_UpdateRemindLatter.Opacity = 0;
    }



    #endregion




    #region Update Content WebView




    private async Task LoadUpdateContentAsync()
    {
        try
        {
            StackPanel_Loading.IsVisible = true;
            StackPanel_Error.IsVisible = false;

            await webview.EnsureCoreWebView2Async();
            string ua = webview.CoreWebView2!.Settings.UserAgent;
            if (!ua.Contains("LunarWard"))
            {
                webview.CoreWebView2.Settings.UserAgent = $"{ua} LunarWard/{AppConfig.AppVersion}";
            }
            webview.CoreWebView2.Profile.PreferredColorScheme = ShouldAppsUseDarkMode() ? Frameworks.WebView.CoreWebView2PreferredColorScheme.Dark : Frameworks.WebView.CoreWebView2PreferredColorScheme.Light;
            webview.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
            webview.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            webview.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
            webview.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            string markdown = await GetReleaseContentMarkdownAsync();
            string html = await RenderMarkdownAsync(markdown);
            webview.NavigateToString(html);
            AppConfig.LastAppVersion = AppConfig.AppVersion;
        }
        catch (InvalidOperationException ex)
        {
            // 上游 catch (COMException)（WebView2 运行时缺失）；WebKitGTK 缺失时本仓库抛 InvalidOperationException。
            _logger.LogError(ex, "Load recent update content");
            TextBlock_Error.Text = Lang.Common_WebView2ComponentInitializationFailed;
            StackPanel_Loading.IsVisible = false;
            StackPanel_Error.IsVisible = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException)
        {
            _logger.LogError(ex, "Load recent update content");
            string tag = NewVersion?.Version ?? AppConfig.AppVersion;
            webview.Source = new Uri($"https://github.com/QiE2035/LunarWard/releases/tag/{tag}");
            webview.IsVisible = true;
            StackPanel_Loading.IsVisible = false;
            StackPanel_Error.IsVisible = false;
            AppConfig.LastAppVersion = AppConfig.AppVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load recent update content");
            TextBlock_Error.Text = Lang.DownloadGamePage_UnknownError;
            StackPanel_Loading.IsVisible = false;
            StackPanel_Error.IsVisible = true;
        }
    }



    private async Task<string> GetReleaseContentMarkdownAsync()
    {
        bool showPrerelease = false;
        NuGetVersion? startVersion, endVersion;
        if (NewVersion is null)
        {
            _ = NuGetVersion.TryParse(AppConfig.LastAppVersion, out startVersion);
            _ = NuGetVersion.TryParse(AppConfig.AppVersion, out endVersion);
        }
        else
        {
            _ = NuGetVersion.TryParse(AppConfig.AppVersion, out startVersion);
            _ = NuGetVersion.TryParse(NewVersion.Version, out endVersion);
        }
        startVersion ??= new NuGetVersion(0, 0, 0);
        endVersion ??= new NuGetVersion(int.MaxValue, int.MaxValue, int.MaxValue);
        if (endVersion.IsPrerelease)
        {
            showPrerelease = true;
            if (startVersion.IsPrerelease)
            {
                if (startVersion.Patch - 1 >= 0)
                {
                    startVersion = new NuGetVersion(startVersion.Major, startVersion.Minor, startVersion.Patch - 1);
                }
                else if (startVersion.Minor - 1 >= 0)
                {
                    startVersion = new NuGetVersion(startVersion.Major, startVersion.Minor - 1, int.MaxValue);
                }
                else if (startVersion.Major - 1 >= 0)
                {
                    startVersion = new NuGetVersion(startVersion.Major - 1, int.MaxValue, int.MaxValue);
                }
            }
        }

        var releases = await _releaseClient.GetGithubReleaseAsync(1, 20);
        var markdown = new StringBuilder();
        int count = 0;
        foreach (var release in releases)
        {
            if (NuGetVersion.TryParse(release.TagName, out var version))
            {
                if (version >= startVersion && version <= endVersion)
                {
                    // 只显示最新的几个连续的预览版，最新稳定版之前的预览版不显示
                    if (!version.IsPrerelease && !release.Prerelease)
                    {
                        showPrerelease = false;
                    }
                    if (!(showPrerelease ^ version.IsPrerelease))
                    {
                        AppendReleaseToStringBuilder(release, markdown);
                        count++;
                    }
                }
            }
            else
            {
                AppendReleaseToStringBuilder(release, markdown);
                count++;
            }
            if (count >= 10)
            {
                break;
            }
        }
        if (markdown.Length == 0)
        {
            try
            {
                var r = await _releaseClient.GetGithubReleaseAsync(NewVersion?.Version ?? AppConfig.AppVersion);
                if (r is not null)
                {
                    AppendReleaseToStringBuilder(r, markdown);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                if (releases.FirstOrDefault() is GithubRelease r)
                {
                    AppendReleaseToStringBuilder(r, markdown);
                }
            }
        }
        return markdown.ToString();
    }



    private static void AppendReleaseToStringBuilder(GithubRelease release, StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"# {release.Name}");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(release.Body);
        stringBuilder.AppendLine("<br>");
        stringBuilder.AppendLine();
    }



    private async Task<string> RenderMarkdownAsync(string markdown)
    {
        string html = await _releaseClient.RenderGithubMarkdownAsync(markdown);
        var cssFile = Path.Combine(AppContext.BaseDirectory, "Assets", "CSS", "github-markdown.css");
        string? css = null;
        if (File.Exists(cssFile))
        {
            css = await File.ReadAllTextAsync(cssFile);
            css = $"<style>{css}</style>";
        }
        else
        {
            css = """<link href="https://cdnjs.cloudflare.com/ajax/libs/github-markdown-css/5.8.1/github-markdown.min.css" type="text/css" rel="stylesheet" />""";
        }
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <base target="_blank">
              {{css}}
              <style>
                @media (prefers-color-scheme: light) {
                  ::-webkit-scrollbar {
                    width: 6px
                  }
                  ::-webkit-scrollbar-thumb {
                    background-color: #b8b8b8;
                    border-radius: 1000px 0px 0px 1000px
                  }
                  ::-webkit-scrollbar-thumb:hover {
                    background-color: #8b8b8b
                  }
                }
                @media (prefers-color-scheme: dark) {
                  ::-webkit-scrollbar {
                    width: 6px
                  }
                  ::-webkit-scrollbar-thumb {
                    background-color: #646464;
                    border-radius: 1000px 0px 0px 1000px
                  }
                  ::-webkit-scrollbar-thumb:hover {
                    background-color: #8b8b8b
                  }
                }
              </style>
            </head>
            <body style="margin: 12px 24px 12px 24px; overflow-x: hidden;">
              <article class="markdown-body" style="background: transparent;">
                {{html}}
              </article>
              <script>
                document.querySelectorAll('img[data-canonical-src]').forEach(img => {
                  const canonical = img.getAttribute('data-canonical-src');
                  if (canonical) {
                    img.src = canonical;
                  }
                  const parent = img.parentElement;
                  if (parent && parent.tagName.toLowerCase() === 'a') {
                    parent.href = canonical;
                  }
                });
              </script>
            </body>
            </html>
            """;
    }



    private void CoreWebView2_DOMContentLoaded(object? sender, object? args)
    {
        webview.Focus();
        webview.IsVisible = true;
        StackPanel_Loading.IsVisible = false;
        StackPanel_Error.IsVisible = false;
    }



    private void CoreWebView2_NewWindowRequested(object? sender, Frameworks.WebView.CoreWebView2NewWindowRequestedEventArgs args)
    {
        try
        {
            _ = Frameworks.ShellHelper.OpenUri(args.Uri);
            args.Handled = true;
        }
        catch { }
    }



    [RelayCommand]
    private async Task RetryAsync()
    {
        await LoadUpdateContentAsync();
    }




    #endregion





    #region Converter



    public static string ByteLengthToString(long byteLength)
    {
        double length = byteLength;
        return length switch
        {
            >= (1 << 30) => $"{length / (1 << 30):F2} GB",
            >= (1 << 20) => $"{length / (1 << 20):F2} MB",
            _ => $"{length / (1 << 10):F2} KB",
        };
    }



    /// <summary>
    /// 上游 XAML 用 <c>local:UpdateWindow.StringToVisibility(...)</c>；Avalonia 侧 XAML 改用内置
    /// <c>StringConverters.IsNotNullOrEmpty</c>（等价），此方法保留以对齐上游公开 API。
    /// </summary>
    public static bool StringToVisibility(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }


    #endregion




    #region INotifyPropertyChanged

    // 上游用 [INotifyPropertyChanged]（WinUI Window 不实现 INPC）；Avalonia 的 Window 已实现 INPC 语义、
    // Toolkit 源生成器无法叠加，因此与 UIGF4GachaWindow/SettingPageBase 一致地手写（docs/09）。

    // 显式接口实现：AvaloniaObject 已有同名的 AvaloniaPropertyChanged 事件（CS0108），
    // 显式实现既避免隐藏，也仍然让 Avalonia 绑定系统走 INotifyPropertyChanged。
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }

    private PropertyChangedEventHandler? _propertyChanged;


    private bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }


    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    /// <summary>上游 <c>this.Bindings.Update()</c> 的等价物：逐个通知一次，刷新 <c>{x:Static}</c> 之外的绑定。</summary>
    private void RefreshBindings()
    {
        OnPropertyChanged(nameof(ChannelText));
        OnPropertyChanged(nameof(ArchitectureText));
        OnPropertyChanged(nameof(PackageSizeText));
    }


    #endregion



}
