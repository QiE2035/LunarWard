using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.HoYoPlay;
using LunarWard.Features.Setting;
using LunarWard.Features.ViewHost;
using LunarWard.Helpers;
using LunarWard.Language;
using LunarWard.Platform;
using LunarWard.Services;

namespace LunarWard.Features.GameSelector;

/// <summary>
/// <b>移植自</b> <c>Starward/src/Starward/Features/GameSelector/GameSelector.xaml(.cs)</c>。
/// 平台替换：XamlRoot/RasterizationScale→TopLevel.Scaling；XamlRoot.SetWindowDragRectangles→空实现（X11 无拖拽区）；
/// 注册表 <c>HYP</c> 键→<see cref="WineRegistry.ReadStringAsync"/>（读当前 prefix，见 <c>AutoSearchInstalledGamesAsync</c>）；
/// <c>GetFileInformationByHandleEx</c> 的 FileIdInfo→<c>stat</c> 的 <c>st_dev/st_ino</c> 判硬链接；
/// <c>FlyoutBase.GetAttachedFlyout(...).ShowAt(options)</c>→<c>FlyoutBase.ShowAttachedFlyout</c> + 自持引用关闭；
/// <c>VisualTreeHelper.GetOpenPopupsForXamlRoot</c>→<see cref="FlyoutBase.Hide"/>。
/// </summary>
public sealed partial class GameSelector : UserControl, INotifyPropertyChanged
{


    public event EventHandler<(GameId, bool DoubleTapped)>? CurrentGameChanged;


    private readonly HoYoPlayService _hoyoplayService = AppConfig.GetService<HoYoPlayService>();


    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();



    public GameSelector()
    {
        this.InitializeComponent();
        InitializeGameSelector();
        this.Loaded += GameSelector_Loaded;
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, OnLanguageChanged);
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, OnMainWindowStateChanged);
        WeakReferenceMessenger.Default.Register<MainWindowDragRectAdaptToGameIconMessage>(this, OnMainWindowStateChanged);
    }



    public GameBiz CurrentGameBiz { get; set; }


    public GameId? CurrentGameId { get; set; }


    public ObservableCollection<GameBizIcon> GameBizIcons { get; set => SetProperty(ref field, value); } = new();


    public GameBizIcon? CurrentGameBizIcon { get; set => SetProperty(ref field, value); }


    public bool IsPinned { get; set => SetProperty(ref field, value); }


    private bool ignoreDpiChanged = false;

    private double lastScale = 1;



    public void InitializeGameSelector()
    {
        List<GameInfo> gameInfos = GetCachedGameInfos();
        InitializeGameIconsArea(gameInfos);
        InitializeGameServerArea(gameInfos);
        InitializeInstalledGamesCommand.Execute(null);
    }



    private async void GameSelector_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            topLevel.ScalingChanged -= TopLevel_ScalingChanged;
            topLevel.ScalingChanged += TopLevel_ScalingChanged;
        }
        await Task.Delay(1000);
        await UpdateGameInfoAsync();
    }



    private void TopLevel_ScalingChanged(object? sender, EventArgs e)
    {
        if (ignoreDpiChanged)
        {
            return;
        }
        double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (lastScale != scale)
        {
            lastScale = scale;
            UpdateDragRectangles();
        }
    }





    private async void OnLanguageChanged(object? _, LanguageChangedMessage __)
    {
        await UpdateGameInfoAsync();
    }




    private async void OnMainWindowStateChanged(object? _, MainWindowStateChangedMessage message)
    {
        try
        {
            if (message.Activate && (message.ElapsedOver(TimeSpan.FromMinutes(10)) || message.IsCrossingHour))
            {
                await UpdateGameInfoAsync();
            }
        }
        catch { }
    }



    private void OnMainWindowStateChanged(object? _, MainWindowDragRectAdaptToGameIconMessage message)
    {
        if (!message.IgnoreDpiChanged)
        {
            UpdateDragRectangles();
        }
        ignoreDpiChanged = message.IgnoreDpiChanged;
    }




    private List<GameInfo> GetCachedGameInfos()
    {
        try
        {
            string? json = AppConfig.CachedGameInfo;
            if (!string.IsNullOrWhiteSpace(json))
            {
                return JsonSerializer.Deserialize<List<GameInfo>>(json) ?? [];
            }
        }
        catch { }
        return [];
    }




    #region Game Icon


    private void InitializeGameIconsArea(List<GameInfo> gameInfos)
    {
        try
        {
            GameBizIcons.CollectionChanged -= GameBizIcons_CollectionChanged;
            GameBizIcons.Clear();

            string? bizs = AppConfig.SelectedGameBizs;
            foreach (string str in bizs?.Split(',')?.Distinct() ?? [])
            {
                if (GameBiz.TryParse(str, out GameBiz biz))
                {
                    GameBizIcons.Add(new GameBizIcon(biz));
                }
                else if (gameInfos.FirstOrDefault(x => x.GameBiz == biz) is GameInfo info)
                {
                    GameBizIcons.Add(new GameBizIcon(info));
                }
            }

            GameBiz lastSelectedGameBiz = AppConfig.CurrentGameBiz;
            if (GameBizIcons.FirstOrDefault(x => x.GameBiz == lastSelectedGameBiz) is GameBizIcon icon)
            {
                CurrentGameBizIcon = icon;
                CurrentGameBizIcon.IsSelected = true;
                CurrentGameBiz = lastSelectedGameBiz;
            }
            else if (lastSelectedGameBiz.IsKnown())
            {
                CurrentGameBizIcon = new GameBizIcon(lastSelectedGameBiz);
                CurrentGameBizIcon.IsSelected = true;
                CurrentGameBiz = lastSelectedGameBiz;
            }
            else if (gameInfos.FirstOrDefault(x => x.GameBiz == lastSelectedGameBiz) is GameInfo info)
            {
                CurrentGameBizIcon = new GameBizIcon(info);
                CurrentGameBizIcon.IsSelected = true;
                CurrentGameBiz = lastSelectedGameBiz;
            }

            CurrentGameId = CurrentGameBizIcon?.GameId;
            if (CurrentGameId is not null)
            {
                CurrentGameChanged?.Invoke(this, (CurrentGameId, false));
            }

            if (AppConfig.IsGameBizSelectorPinned)
            {
                Pin();
            }

            GameBizIcons.CollectionChanged += GameBizIcons_CollectionChanged;
        }
        catch { }
        if (GameBizIcons.Count == 0 && CurrentGameBizIcon is null)
        {
            TeachTip_SelectGame.IsOpen = true;
        }
    }



    public bool GameIconsAreaVisible
    {
        get => _gameIconsAreaVisible;
        set
        {
            _gameIconsAreaVisible = value;
            Grid_GameIconsArea.RenderTransform = value ? null : new TranslateTransform(0, -100);
            Grid_GameIconsArea.Opacity = value ? 1 : 0;
            Grid_GameIconsArea.IsHitTestVisible = value;
            UpdateDragRectangles();
        }
    }

    private bool _gameIconsAreaVisible;


    /// <summary>
    /// 更新窗口拖拽区域。
    /// 平台替换：X11 没有 AppWindow.TitleBar.SetDragRectangles 的等价物，
    /// 这里只保留方法以便与上游调用点一一对应，实际由外壳窗口整体处理。
    /// </summary>
    public void UpdateDragRectangles()
    {
        if (ignoreDpiChanged)
        {
            return;
        }
        try
        {
            double x = Border_CurrentGameIcon.Bounds.Width;
            if (GameIconsAreaVisible)
            {
                x = Border_CurrentGameIcon.Bounds.Width + Grid_GameIconsArea.Bounds.Width;
            }
            this.FindTopLevel()?.SetWindowDragRectangles([new Rect(x, 0, 10000, 48)]);
        }
        catch { }
    }



    private void Border_CurrentGameIcon_PointerEntered(object? sender, PointerEventArgs e)
    {
        GameIconsAreaVisible = true;
    }



    private void Border_CurrentGameIcon_PointerExited(object? sender, PointerEventArgs e)
    {
        if (FullBackgroundVisible || IsPinned)
        {
            return;
        }
        if (sender is Control ele)
        {
            var postion = e.GetPosition(ele);
            if (postion.X > ele.Bounds.Width - 1 && postion.Y > 0 && postion.Y < ele.Bounds.Height)
            {
                return;
            }
        }
        GameIconsAreaVisible = false;
    }



    private void Grid_GameIconsArea_PointerExited(object? sender, PointerEventArgs e)
    {
        if (FullBackgroundVisible || IsPinned)
        {
            return;
        }
        GameIconsAreaVisible = false;
    }



    private void Grid_GameIcon_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control fe && fe.DataContext is GameBizIcon icon)
        {
            if (CurrentGameBizIcon is not null)
            {
                CurrentGameBizIcon.IsSelected = false;
            }

            CurrentGameBizIcon = icon;
            CurrentGameBiz = icon.GameBiz;
            CurrentGameId = icon.GameId;
            icon.IsSelected = true;

            CurrentGameChanged?.Invoke(this, (icon.GameId, false));
            AppConfig.CurrentGameBiz = icon.GameBiz;
        }
    }



    private void Grid_GameIcon_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control fe && fe.DataContext is GameBizIcon icon)
        {
            if (CurrentGameBizIcon is not null)
            {
                CurrentGameBizIcon.IsSelected = false;
            }

            CurrentGameBizIcon = icon;
            CurrentGameBiz = icon.GameBiz;
            CurrentGameId = icon.GameId;
            icon.IsSelected = true;
            HideFullBackground();

            CurrentGameChanged?.Invoke(this, (icon.GameId, true));
            AppConfig.CurrentGameBiz = icon.GameBiz;
        }
    }


    private void Grid_GameIcon_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control fe && fe.DataContext is GameBizIcon icon)
        {
            if (!icon.IsSelected)
            {
                icon.MaskOpacity = 0;
            }
        }
    }



    private void Grid_GameIcon_PointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control fe && fe.DataContext is GameBizIcon icon)
        {
            if (!icon.IsSelected)
            {
                icon.MaskOpacity = 1;
            }
        }
    }



    private void Grid_GameIconsArea_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateDragRectangles();
    }



    [RelayCommand]
    private void Pin()
    {
        IsPinned = !IsPinned;
        if (IsPinned)
        {
            GameIconsAreaVisible = true;
        }
        else
        {
            var temp = CurrentGameBizIcon;
            CurrentGameBizIcon = null;
            CurrentGameBizIcon = temp;
            if (!FullBackgroundVisible)
            {
                GameIconsAreaVisible = false;
            }
        }
        AppConfig.IsGameBizSelectorPinned = IsPinned;
    }



    private void GameBizIcons_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var icon in GameBizIcons)
            {
                sb.Append(icon.GameBiz);
                sb.Append(',');
            }
            AppConfig.SelectedGameBizs = sb.ToString().TrimEnd(',');
        }
        catch { }
    }



    #endregion





    #region Full Background 黑色半透明背景


    public bool FullBackgroundVisible => Border_FullBackground.Opacity > 0;


    [RelayCommand]
    private void ShowFullBackground()
    {
        Border_FullBackground.Opacity = 1;
        Border_FullBackground.IsHitTestVisible = true;
        Border_FullBackground.IsVisible = true;
        Border_Pin.Opacity = 1;
        Border_Pin.IsHitTestVisible = true;
        Border_Pin.IsVisible = true;
        GameIconsAreaVisible = true;
    }


    private void HideFullBackground()
    {
        Border_FullBackground.Opacity = 0;
        Border_FullBackground.IsHitTestVisible = false;
        Border_FullBackground.IsVisible = false;
        Border_Pin.Opacity = 0;
        Border_Pin.IsHitTestVisible = false;
        Border_Pin.IsVisible = false;
        if (!IsPinned)
        {
            GameIconsAreaVisible = false;
        }
    }


    private void Border_FullBackground_Tapped(object? sender, TappedEventArgs e)
    {
        _isGameBizDisplayPressed = false;
        var position = e.GetPosition(sender as Control);
        if (position.X <= Border_CurrentGameIcon.Bounds.Width && position.Y <= Border_CurrentGameIcon.Bounds.Height)
        {
            Border_FullBackground.Opacity = 0;
            Border_FullBackground.IsHitTestVisible = false;
            Border_FullBackground.IsVisible = false;
            Border_Pin.Opacity = 0;
            Border_Pin.IsHitTestVisible = false;
            Border_Pin.IsVisible = false;
        }
        else
        {
            HideFullBackground();
        }
    }


    #endregion





    #region Game Server



    public List<GameBizDisplay> GameBizDisplays { get; set => SetProperty(ref field, value); } = new();



    private void InitializeGameServerArea(List<GameInfo> gameInfos)
    {
        try
        {
            var list = new List<GameBizDisplay>();

            if (LanguageUtil.FilterLanguage(CultureInfo.CurrentUICulture.Name) is "zh-cn")
            {
                foreach (var info in gameInfos)
                {
                    if (info.GameBiz.IsChinaServer() && !info.IsBilibiliServer())
                    {
                        list.Add(new GameBizDisplay { GameInfo = info });
                    }
                }
            }
            else
            {
                foreach (var info in gameInfos)
                {
                    if (info.GameBiz.IsGlobalServer())
                    {
                        list.Add(new GameBizDisplay { GameInfo = info });
                    }
                }
            }

            foreach (var item in list)
            {
                string game = item.GameInfo.GameBiz.Game;
                foreach (string suffix in (string[])["_cn", "_global", "_bilibili"])
                {
                    GameBiz biz = game + suffix;
                    if (biz.IsKnown())
                    {
                        var server = new GameBizIcon(biz)
                        {
                            IsPinned = GameBizIcons.Any(x => x.GameBiz == biz),
                        };
                        item.Servers.Add(server);
                    }
                    else if (gameInfos.FirstOrDefault(x => x.GameBiz == biz) is GameInfo info)
                    {
                        var server = new GameBizIcon(info)
                        {
                            IsPinned = GameBizIcons.Any(x => x.GameBiz == biz),
                        };
                        item.Servers.Add(server);
                    }
                }
            }
            GameBizDisplays = new(list);
        }
        catch { }
    }



    private async Task UpdateGameInfoAsync()
    {
        try
        {
            List<GameInfo> gameInfos = await _hoyoplayService.UpdateGameInfoListAsync();
            InitializeGameServerArea(gameInfos);
            foreach (GameBizIcon icon in GameBizIcons)
            {
                if (icon.GameBiz.IsKnown())
                {
                    icon.UpdateInfo();
                }
                else
                {
                    GameInfo info = await _hoyoplayService.GetGameInfoAsync(icon.GameId);
                    icon.UpdateInfo(info);
                }
            }
            await InitializeInstalledGamesCommand.ExecuteAsync(null);
        }
        catch { }
    }




    private bool _isGameBizDisplayPressed = false;

    private FlyoutBase? _currentFlyout;


    private void Grid_GameBizDisplay_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control ele)
        {
            e.Handled = true;
            _isGameBizDisplayPressed = !_isGameBizDisplayPressed;
            _currentFlyout?.Hide();
            _currentFlyout = FlyoutBase.GetAttachedFlyout(ele);
            FlyoutBase.ShowAttachedFlyout(ele);
        }
    }


    private void Grid_GameBizDisplay_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control ele && _isGameBizDisplayPressed)
        {
            _currentFlyout?.Hide();
            _currentFlyout = FlyoutBase.GetAttachedFlyout(ele);
            FlyoutBase.ShowAttachedFlyout(ele);
        }
    }


    private void Button_GameServer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (sender is Control fe && fe.DataContext is GameBizIcon server)
            {
                if (CurrentGameBizIcon is not null)
                {
                    CurrentGameBizIcon.IsSelected = false;
                }

                if (GameBizIcons.FirstOrDefault(x => x.GameId == server.GameId) is GameBizIcon icon)
                {
                    CurrentGameBizIcon = icon;
                    CurrentGameBiz = icon.GameBiz;
                    CurrentGameId = icon.GameId;
                    icon.IsSelected = true;
                }
                else
                {
                    CurrentGameBizIcon = server;
                    server.IsSelected = true;
                }

                CurrentGameChanged?.Invoke(this, (server.GameId, false));
                // 关闭弹出的服务器选择菜单
                _currentFlyout?.Hide();
                HideFullBackground();
                _isGameBizDisplayPressed = false;
                AppConfig.CurrentGameBiz = server.GameBiz;
            }
        }
        catch { }
    }


    private void Button_PinGameBiz_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control fe && fe.DataContext is GameBizIcon server)
        {
            var biz = server.GameBiz;
            if (GameBizIcons.FirstOrDefault(x => x.GameBiz == biz) is GameBizIcon icon)
            {
                GameBizIcons.Remove(icon);
                server.IsPinned = false;
            }
            else
            {
                GameBizIcons.Add(server);
                server.IsPinned = true;
            }
        }
    }




    #endregion





    #region Installed Games


    public string? InstalledGamesActualSize { get; set => SetProperty(ref field, value); }


    public string? InstalledGamesSavedSize { get; set => SetProperty(ref field, value); }




    public ObservableCollection<GameBizIcon> InstalledGames { get; set; } = new();



    private CancellationTokenSource? _initializeInstalledGamesCancellationTokenSource;


    [RelayCommand]
    private async Task InitializeInstalledGamesAsync()
    {
        const double GB = 1 << 30;
        try
        {
            _initializeInstalledGamesCancellationTokenSource?.Cancel();
            _initializeInstalledGamesCancellationTokenSource = new();
            CancellationToken token = _initializeInstalledGamesCancellationTokenSource.Token;

            InstalledGames.Clear();
            InstalledGamesActualSize = null;
            InstalledGamesSavedSize = null;
            List<FileInfo> files = new();

            foreach (GameBizDisplay display in GameBizDisplays)
            {
                List<FileInfo> _duplicateFiles = new();
                int serverCount = 0;
                foreach (GameBizIcon server in display.Servers)
                {
                    string? installPath = GameLauncherService.GetGameInstallPath(server.GameId);
                    if (Directory.Exists(installPath))
                    {
                        server.InstallPath = installPath;
                        var _files = new DirectoryInfo(installPath).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
                        server.TotalSize = _files.Sum(x => x.Length);
                        InstalledGames.Add(server);
                        if (_files.Count() > 0)
                        {
                            serverCount++;
                            _duplicateFiles.AddRange(_files);
                        }
                    }
                }
                if (serverCount > 1)
                {
                    files.AddRange(_duplicateFiles);
                }
            }
            long totalSize = InstalledGames.Sum(x => x.TotalSize);
            InstalledGamesActualSize = $"{totalSize / GB:F2}GB";

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (files.Count > 0)
            {
                (long fileSize, long actualSize) = await Task.Run(() =>
                {
                    long size = 0;
                    Dictionary<(ulong Dev, ulong Ino), long> dic = new();
                    foreach (var file in files)
                    {
                        size += file.Length;
                        if (FileIdentity.TryGet(file.FullName, out ulong dev, out ulong ino))
                        {
                            dic[(dev, ino)] = file.Length;
                        }
                        else
                        {
                            dic[((ulong)file.FullName.GetHashCode(), (ulong)file.Length)] = file.Length;
                        }
                    }
                    return (size, dic.Values.Sum());
                }, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                InstalledGamesActualSize = $"{(totalSize - fileSize + actualSize) / GB:F2}GB";
                InstalledGamesSavedSize = $"{(fileSize - actualSize) / GB:F2}GB";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }




    /// <summary>
    /// 从已保存的安装路径与 HYP 注册表自动搜索已安装的游戏。
    /// </summary>
    /// <remarks>
    /// 平台替换（docs/09 §12.1、docs/14）：
    /// <list type="bullet">
    ///   <item>上游 <c>Registry.GetValue(@"HKEY_CURRENT_USER\Software\miHoYo\HYP\1_1\{biz}", "GameInstallPath")</c>
    ///   → <see cref="WineRegistry.ReadStringAsync"/> 读取当前 prefix 的 HYP 键（prefix 复用
    ///   <see cref="GamePrefixContext.CurrentPrefix"/>，不另写一套）。</item>
    ///   <item>上游同步方法 → <c>async Task</c>（wine 子进程只能异步，阻塞会冻 UI）。</item>
    /// </list>
    /// </remarks>
    [RelayCommand]
    public async Task AutoSearchInstalledGamesAsync()
    {
        try
        {
            List<GameInfo> gameInfos = GetCachedGameInfos();
            var sb = new StringBuilder();
            foreach (GameInfo item in gameInfos)
            {
                GameBiz gameBiz = item.GameBiz;
                if (item.IsBilibiliServer())
                {
                    gameBiz = $"{gameBiz.Game}_bilibili";
                }
                string? path = GameLauncherService.GetGameInstallPath(gameBiz);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    if (Directory.Exists(path) || AppConfig.GetGameInstallPathRemovable(gameBiz))
                    {
                        sb.Append(gameBiz);
                        sb.Append(',');
                        continue;
                    }
                }

                string key = "";
                if (gameBiz.Server is "cn")
                {
                    key = $@"HKEY_CURRENT_USER\Software\miHoYo\HYP\1_1\{gameBiz}";
                }
                else if (gameBiz.Server is "global")
                {
                    key = $@"HKEY_CURRENT_USER\Software\Cognosphere\HYP\1_0\{gameBiz}";
                }
                if (!string.IsNullOrWhiteSpace(key))
                {
                    path = await ReadHypInstallPathAsync(key);
                    if (Directory.Exists(path))
                    {
                        AppConfig.SetGameInstallPath(gameBiz, path);
                        sb.Append(gameBiz);
                        sb.Append(',');
                        continue;
                    }
                }
            }
            AppConfig.SelectedGameBizs = sb.ToString().TrimEnd(',');
            InitializeGameSelector();
            if (!IsPinned)
            {
                Pin();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }




    /// <summary>
    /// 读 HYP 键里的 <c>GameInstallPath</c>（REG_SZ）。prefix 与 wine 二进制都复用
    /// <see cref="GamePrefixContext"/>；未选档案或注册表写的是 Windows 路径时返回 <c>null</c>。
    /// </summary>
    private static async Task<string?> ReadHypInstallPathAsync(string key)
    {
        string? prefix = GamePrefixContext.CurrentPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        WineRegistry registry = new(UnixProcessLauncher.Default, AppConfig.GetService<LogService>());
        string? value = await registry.ReadStringAsync(
            GamePrefixContext.CurrentWineBinary ?? "wine",
            prefix,
            key,
            "GameInstallPath").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // HYP 可能保存 Windows 风格路径（C:\...），Linux 下换算成 prefix 内的真实路径。
        if (value.Length > 2 && value[1] == ':' && (value[2] == '\\' || value[2] == '/'))
        {
            string drive = char.ToLowerInvariant(value[0]).ToString();
            string rest = value[2..].Replace('\\', '/').TrimStart('/');
            string candidate = Path.Join(prefix, "dosdevices", drive + ":", rest);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            return null;
        }

        return value;
    }




    private void Expander_InstalledGamesActualSize_Tapped(object? sender, TappedEventArgs e)
    {
        e.Handled = true;
    }



    #endregion



    public new event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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


}


/// <summary>
/// Linux 硬链接识别：上游用 <c>GetFileInformationByHandleEx(FileIdInfo)</c>，
/// 这里用 <c>stat</c> 的 <c>st_dev</c>(offset 0) + <c>st_ino</c>(offset 8)（x86_64/aarch64 同布局）。
/// </summary>
internal static class FileIdentity
{

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int StatNative(string path, byte[] buffer);

    public static bool TryGet(string path, out ulong device, out ulong inode)
    {
        device = 0;
        inode = 0;
        try
        {
            var buffer = new byte[512];
            if (StatNative(path, buffer) != 0)
            {
                return false;
            }
            device = BitConverter.ToUInt64(buffer, 0);
            inode = BitConverter.ToUInt64(buffer, 8);
            return true;
        }
        catch
        {
            return false;
        }
    }

}
