using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.Gacha;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.GamepadControl;
using LunarWard.Features.GameRecord;
using LunarWard.Features.GameSetting;
using LunarWard.Features.RPC;
using LunarWard.Features.Screenshot;
using LunarWard.Features.SelfQuery;
using LunarWard.Features.Setting;
using LunarWard.Features.Update;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using LunarWard.Language;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.ViewHost;

/// <summary>
/// 上游 <c>Starward.Features.ViewHost.MainView</c> 的 1:1 移植。
/// Avalonia 差异：<see cref="Frame"/> / <see cref="NavigationView"/> 为等价实现（docs/09 §五）；
/// <c>Visibility</c> → <c>IsVisible</c>；<c>DispatcherQueue</c> → <see cref="Dispatcher"/>；
/// <c>XamlRoot.GetWindowHandle()</c> → <see cref="XamlRootExtension"/> 的 TopLevel 扩展。
/// </summary>
public sealed partial class MainView : UserControl, INotifyPropertyChanged
{

    private readonly ILogger<MainView> _logger = AppConfig.GetLogger<MainView>();

    private GameId? _currentGameId;

    private DateTimeOffset _lastCheckUpdateTime;

    private DateTimeOffset _lastShowUpdateTime;

    private readonly SemaphoreSlim _updateLock = new(1, 1);


    public event PropertyChangedEventHandler? PropertyChanged;


    public GameId? CurrentGameId
    {
        get => _currentGameId;
        private set
        {
            if (!Equals(_currentGameId, value))
            {
                _currentGameId = value;
                OnPropertyChanged();
                // AppBackground.CurrentGameId 是普通 CLR 属性（非 AvaloniaProperty），不能走 CompiledBinding，
                // 这里手工同步（上游 XAML 用 {x:Bind}，Avalonia 的生成式绑定要求 StyledProperty）。
                AppBackground.CurrentGameId = value;
            }
        }
    }


    // 上游 GameFeatureConfig 为 internal（同程序集可见），属性改为 internal 保持可访问性一致
    internal GameFeatureConfig CurrentGameFeatureConfig { get; private set; } = GameFeatureConfig.FromGameId(null);


    public MainView()
    {
        DataContext = this;
        InitializeComponent();
        InitializeMainView();
    }


    private void InitializeMainView()
    {
        Loaded += MainView_Loaded;
        GameId? gameId = GameSelector.CurrentGameId;
        if (gameId?.GameBiz == GameBiz.bh3_global)
        {
            string? id = AppConfig.LastGameIdOfBH3Global;
            if (!string.IsNullOrWhiteSpace(id))
            {
                gameId.Id = id;
            }
        }
        CurrentGameId = gameId;
        CurrentGameFeatureConfig = GameFeatureConfig.FromGameId(CurrentGameId);
        UpdateNavigationView();
        WeakReferenceMessenger.Default.Register<MainViewNavigateMessage>(this, OnMainViewNavigateMessageReceived);
        WeakReferenceMessenger.Default.Register<BH3GlobalGameServerChangedMessage>(this, OnBH3GlobalGameServerChanged);
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, (_, _) => _ = CheckUpdateOrShowRecentUpdateContentAsync());
    }


    private async void MainView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CheckSystemProxy();
        HotkeyManager.InitializeHotkey(this.FindTopLevel()?.TryGetPlatformHandle()?.Handle ?? 0);
        _ = CheckUpdateOrShowRecentUpdateContentAsync();
        AppConfig.GetService<RpcService>().TrySetEnviromentAsync();
        LogUploadService.Start();
        if (AppConfig.EnableGamepadController)
        {
            await Task.Delay(1000);
            Dispatcher? dispatcher = Dispatcher.UIThread;
            _ = Task.Run(() => GamepadController.Initialize(dispatcher));
        }
    }


    private void GameSelector_CurrentGameChanged(object? sender, (GameId, bool DoubleTapped) e)
    {
        if (e.Item1.GameBiz == GameBiz.bh3_global)
        {
            // 崩坏3国际服区服
            string? id = AppConfig.LastGameIdOfBH3Global;
            if (!string.IsNullOrWhiteSpace(id))
            {
                e.Item1.Id = id;
            }
        }
        CurrentGameId = e.Item1;
        CurrentGameFeatureConfig = GameFeatureConfig.FromGameId(CurrentGameId);
        UpdateNavigationView();
    }


    private void OnBH3GlobalGameServerChanged(object? recipient, BH3GlobalGameServerChangedMessage message)
    {
        if (CurrentGameId?.GameBiz == GameBiz.bh3_global)
        {
            CurrentGameId!.Id = message.GameId;
            OnPropertyChanged(nameof(CurrentGameId));
            NavigateTo(typeof(GameLauncherPage), CurrentGameId, new SuppressNavigationTransitionInfo());
        }
    }


    #region Navigation


    private void UpdateNavigationView()
    {
        NavigationViewItem_Launcher.IsVisible = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GameLauncherPage));
        NavigationViewItem_GameSetting.IsVisible = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GameSettingPage));
        NavigationViewItem_Screenshot.IsVisible = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(ScreenshotPage));
        NavigationViewItem_GachaLog.IsVisible = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GachaLogPage));
        NavigationViewItem_HoyolabToolbox.IsVisible = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GameRecordPage));
        NavigationViewItem_SelfQuery.IsVisible = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(SelfQueryPage));
        NavigationViewItem_GenshinBeyondGacha.IsVisible = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GenshinBeyondGachaPage));

        // 工具箱名称（上游是 Titan 工具 / HoYoLAB 工具）
        if (CurrentGameId?.GameBiz.IsChinaServer() ?? false)
        {
            ToolTip.SetTip(NavigationViewItem_HoyolabToolbox, Lang.HyperionToolbox);
        }
        if (CurrentGameId?.GameBiz.IsGlobalServer() ?? false)
        {
            ToolTip.SetTip(NavigationViewItem_HoyolabToolbox, Lang.HoYoLABToolbox);
        }

        if (CurrentGameId is null)
        {
            NavigateTo(typeof(BlankPage));
        }
        else if (MainView_Frame.SourcePageType?.Name is not nameof(SettingPage))
        {
            NavigateTo(MainView_Frame.SourcePageType);
        }
    }


    private void NavigationView_ItemInvoked(object? sender, NavigationViewItemInvokedEventArgs args)
    {
        try
        {
            if (args.InvokedItemContainer is not { } item)
            {
                return;
            }
            var type = item.Tag switch
            {
                nameof(GameLauncherPage) => typeof(GameLauncherPage),
                nameof(GameSettingPage) => typeof(GameSettingPage),
                nameof(ScreenshotPage) => typeof(ScreenshotPage),
                nameof(GachaLogPage) => typeof(GachaLogPage),
                nameof(GameRecordPage) => typeof(GameRecordPage),
                nameof(SelfQueryPage) => typeof(SelfQueryPage),
                nameof(GenshinBeyondGachaPage) => typeof(GenshinBeyondGachaPage),
                _ => null,
            };
            NavigateTo(type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NavigationView item invoked");
        }
    }


    /// <summary>
    /// 自检（<c>--screenshot --page …</c>）用：把 <see cref="NavigateTo"/> 暴露给宿主窗口。
    /// 上游没有这条路径（截图靠 UI 自动化驱动），这里只做等价的最小入口，不参与业务流程。
    /// </summary>
    internal void NavigateToForCapture(Type page) => NavigateTo(page);


    private void NavigateTo(Type? page, object? param = null, NavigationTransitionInfo? infoOverride = null)
    {
        page ??= typeof(GameLauncherPage);
        if (page.Name is not nameof(BlankPage) and not nameof(SettingPage) && !CurrentGameFeatureConfig.SupportedPages.Contains(page.Name!))
        {
            page = typeof(GameLauncherPage);
        }
        if (page.Name is nameof(GameLauncherPage))
        {
            MainView_NavigationView.SelectedItem = NavigationViewItem_Launcher;
        }
        MainView_Frame.Navigate(page, param ?? CurrentGameId, infoOverride);
        Border_OverlayMask.Opacity = page.Name is nameof(BlankPage) or nameof(GameLauncherPage) ? 0 : 1;
    }


    private void OnMainViewNavigateMessageReceived(object? recipient, MainViewNavigateMessage message)
    {
        NavigateTo(message.Page);
    }


    #endregion


    #region Update


    private async Task CheckUpdateOrShowRecentUpdateContentAsync()
    {
#if DEBUG || DONOT_CHECK_UPDATE
        return;
#endif
        if (!await _updateLock.WaitAsync(0))
        {
            return;
        }
        await Task.Delay(1000);
        try
        {
            if (_lastCheckUpdateTime == default && NuGetVersion.TryParse(AppConfig.AppVersion, out var appVersion))
            {
                _ = NuGetVersion.TryParse(AppConfig.LastAppVersion, out var lastVersion);
                if (appVersion != lastVersion)
                {
                    if (AppConfig.ShowUpdateContentAfterUpdateRestart)
                    {
                        new UpdateWindow().Activate();
                    }
                    else
                    {
                        AppConfig.LastAppVersion = AppConfig.AppVersion;
                    }
                    _lastCheckUpdateTime = DateTimeOffset.Now - TimeSpan.FromMinutes(55);
                    return;
                }
            }
            DateTimeOffset now = DateTimeOffset.Now;
            if (now - _lastCheckUpdateTime > TimeSpan.FromHours(1))
            {
                var release = await AppConfig.GetService<UpdateService>().CheckUpdateAsync(false);
                _lastCheckUpdateTime = now;
                if (release != null && now - _lastShowUpdateTime > TimeSpan.FromHours(6) && now.Date != _lastShowUpdateTime.Date)
                {
                    new UpdateWindow { NewVersion = release }.Activate();
                    _lastShowUpdateTime = now;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check update");
        }
        finally
        {
            _updateLock.Release();
        }
    }


    #endregion


    private async void CheckSystemProxy()
    {
        try
        {
            await Task.Delay(1500);
            Uri? proxy = HttpClient.DefaultProxy.GetProxy(new Uri("https://starward.scighost.com"));
            if (proxy is not null)
            {
                InAppToast.MainWindow?.Information(Lang.MainView_CheckSystemProxy_SystemProxyIsEnabled, proxy.ToString(), 5000);
            }
        }
        catch { }
    }


    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
