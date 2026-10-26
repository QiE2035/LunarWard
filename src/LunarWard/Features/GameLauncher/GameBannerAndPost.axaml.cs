using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.HoYoPlay;
using LunarWard.Features.ViewHost;
using LunarWard.Frameworks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 启动页右侧的公告/活动卡片（横幅轮播 + 公告页签）。<b>移植自</b>
/// <c>Starward/src/Starward/Features/GameLauncher/GameBannerAndPost.xaml(.cs)</c>。
/// </summary>
/// <remarks>
/// <para>控件替换：<c>FlipView</c>→<c>Carousel</c>、<c>Pivot</c>→<c>TabControl</c>、
/// <c>ItemsRepeater</c>+<c>StackLayout</c>→<c>ItemsControl</c>+<c>StackPanel</c>、
/// <c>Hyperlink</c>→<c>TextBlock</c>+<c>PointerPressed</c>、<c>DispatcherQueueTimer</c>→<c>DispatcherTimer</c>。</para>
/// <para><b>平台替换</b>：<c>XamlRoot.GetWindowHandle()</c>→<c>WindowEx.WindowHandle</c>；
/// <c>User32.SetForegroundWindow</c>→无对应（Avalonia 不提供），改为 <c>Window.Activate()</c>；
/// <c>Launcher.LaunchUriAsync</c>→<c>TopLevel.Launcher</c>。</para>
/// </remarks>
public sealed partial class GameBannerAndPost : UserControl, System.ComponentModel.INotifyPropertyChanged
{

    public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    private bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }


    private readonly DispatcherTimer _bannerTimer;


    private readonly ILogger<GameBannerAndPost> _logger = AppConfig.GetLogger<GameBannerAndPost>();


    private readonly HoYoPlayService _hoYoPlayService = AppConfig.GetService<HoYoPlayService>();


    private readonly GameNoticeService _gameNoticeService = AppConfig.GetService<GameNoticeService>();


    public GameId CurrentGameId { get; set; }



    public GameBannerAndPost()
    {
        this.InitializeComponent();
        this.Loaded += GameBannerAndPost_Loaded;
        this.Unloaded += GameBannerAndPost_Unloaded;
        _bannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _bannerTimer.Tick += _bannerTimer_Tick;
    }




    private async void GameBannerAndPost_Loaded(object? sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, OnMainWindowStateChanged);
        WeakReferenceMessenger.Default.Register<GameNoticeWindowClosedMessage>(this, OnGameNoticeWindowClosed);
        WeakReferenceMessenger.Default.Register<GameAnnouncementSettingChangedMessage>(this, OnGameAnnouncementSettingChanged);
        await UpdateGameContentAsync();
        await UpdateGameNoticeAlertAsync();
    }


    private void GameBannerAndPost_Unloaded(object? sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _bannerTimer.Stop();
        _bannerTimer.Tick -= _bannerTimer_Tick;
        Banners = null;
        PostGroups = null;
    }



    private void OnMainWindowStateChanged(object recipient, MainWindowStateChangedMessage message)
    {
        try
        {
            if (message.Activate)
            {
                _bannerTimer.Start();
            }
            else if (message.Hide || message.SessionLock)
            {
                _bannerTimer.Stop();
            }
        }
        catch { }
    }



    private async void OnGameNoticeWindowClosed(object recipient, GameNoticeWindowClosedMessage message)
    {
        // 平台替换：上游 User32.SetForegroundWindow 在本平台无对应，Avalonia 用 Window.Activate()
        (TopLevel.GetTopLevel(this) as Window)?.Activate();
        await UpdateGameNoticeAlertAsync();
    }



    private async void OnGameAnnouncementSettingChanged(object recipient, GameAnnouncementSettingChangedMessage message)
    {
        if (AppConfig.EnableBannerAndPost)
        {
            ShowBannerAndPost = true;
            await UpdateGameContentAsync();
            if (AppConfig.DisableGameNoticeRedHot)
            {
                IsGameNoticesAlert = false;
            }
            else
            {
                await UpdateGameNoticeAlertAsync();
            }
        }
        else
        {
            ShowBannerAndPost = false;
        }
    }



    private List<GameBanner>? _banners;
    public List<GameBanner>? Banners
    {
        get => _banners;
        set => SetProperty(ref _banners, value);
    }


    private List<GamePostGroup>? _postGroups;
    public List<GamePostGroup>? PostGroups
    {
        get => _postGroups;
        set => SetProperty(ref _postGroups, value);
    }


    private bool _isGameNoticesAlert;
    public bool IsGameNoticesAlert
    {
        get => _isGameNoticesAlert;
        set => SetProperty(ref _isGameNoticesAlert, value);
    }


    private int _selectedPostGroupIndex;
    public int SelectedPostGroupIndex
    {
        get => _selectedPostGroupIndex;
        set => SetProperty(ref _selectedPostGroupIndex, value);
    }


    private string _currentBannerIndexText = "1";
    public string CurrentBannerIndexText
    {
        get => _currentBannerIndexText;
        set => SetProperty(ref _currentBannerIndexText, value);
    }




    public bool ShowBannerAndPost
    {
        get => this.Opacity == 1;
        set
        {
            if (value && Banners?.Count > 0 && PostGroups?.Count > 0)
            {
                _bannerTimer.Start();
                this.Opacity = 1;
                this.IsHitTestVisible = true;
            }
            else
            {
                _bannerTimer.Stop();
                this.Opacity = 0;
                this.IsHitTestVisible = false;
            }
        }
    }




    private async Task UpdateGameContentAsync()
    {
        try
        {
            var content = await _hoYoPlayService.GetGameContentAsync(CurrentGameId);
            if (content is null || !AppConfig.EnableBannerAndPost)
            {
                ShowBannerAndPost = false;
                return;
            }
            Banners = content.Banners;
            PostGroups = GamePostGroup.FromGameContent(content);
            ShowBannerAndPost = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get game launcher content ({CurrentGameId})", CurrentGameId);
        }
    }




    private async Task UpdateGameNoticeAlertAsync()
    {
        try
        {
            if (GameFeatureConfig.FromGameId(CurrentGameId).InGameNoticesWindow)
            {
                Button_InGameNotices.IsVisible = true;
            }
            else
            {
                Button_InGameNotices.IsVisible = false;
                return;
            }
            if (AppConfig.DisableGameNoticeRedHot)
            {
                IsGameNoticesAlert = false;
            }
            else
            {
                IsGameNoticesAlert = await _gameNoticeService.IsNoticeAlertAsync(CurrentGameId.GameBiz);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get game launcher content ({CurrentGameId})", CurrentGameId);
        }
    }




    private void _bannerTimer_Tick(object? sender, EventArgs args)
    {
        try
        {
            if (Banners?.Count > 0)
            {
                FlipView_Banner.SelectedIndex = (FlipView_Banner.SelectedIndex + 1) % Banners.Count;
                CurrentBannerIndexText = AddOne(FlipView_Banner.SelectedIndex);
            }
        }
        catch { }
    }



    private async void Image_Banner_Tapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (sender is Control fe && fe.DataContext is GameBanner banner)
            {
                await LaunchUriAsync(new Uri(banner.Image.Link));
            }
        }
        catch { }
    }



    private async void PostTitle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (sender is Control fe && fe.Tag is string link && !string.IsNullOrWhiteSpace(link))
            {
                await LaunchUriAsync(new Uri(link));
            }
        }
        catch { }
    }


    private async Task LaunchUriAsync(Uri uri)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher is not null)
        {
            await launcher.LaunchUriAsync(uri);
        }
    }



    /// <summary>
    /// 隐藏 Carousel 中自动出现的翻页按键（上游为 FlipView 的翻页按钮）。
    /// </summary>
    private void FlipView_Banner_Loaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var child in FlipView_Banner.GetVisualDescendants())
            {
                if (child is Button button)
                {
                    button.IsHitTestVisible = false;
                    button.Opacity = 0;
                }
            }
        }
        catch { }
    }




    private void Grid_BannerContainer_PointerEntered(object? sender, PointerEventArgs e)
    {
        _bannerTimer.Stop();
        Border_PipsPager.IsVisible = true;
    }



    private void Grid_BannerContainer_PointerExited(object? sender, PointerEventArgs e)
    {
        _bannerTimer.Start();
        Border_PipsPager.IsVisible = false;
    }




    [RelayCommand]
    private void OpenGameNoticeWindow()
    {
        try
        {
            new GameNoticeWindow
            {
                CurrentGameBiz = CurrentGameId.GameBiz,
                ParentWindowHandle = (TopLevel.GetTopLevel(this) as WindowEx)?.WindowHandle ?? 0,
            }.Activate();
        }
        catch { }
    }




    public static string AddOne(int number)
    {
        return (number + 1).ToString();
    }


}
