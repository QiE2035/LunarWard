using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using LunarWard.Core;
using LunarWard.Core.Gacha;
using LunarWard.Core.GameRecord;
using LunarWard.Features.Gacha.UIGF;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.GameRecord;
using LunarWard.Frameworks;
using LunarWard.Features.ViewHost;
using LunarWard.Helpers;
using LunarWard.Services;
using InAppToast = LunarWard.Helpers.InAppToast;
using LunarWard.Views.Starward.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


namespace LunarWard.Features.Gacha;

/// <summary>
/// 上游 <c>Starward.Features.Gacha.GachaLogPage</c> 的逐成员移植。
/// </summary>
/// <remarks>
/// 平台差异（docs/09）：
/// WinUI <c>Page</c>/<c>NavigationEventArgs</c> → <see cref="PageBase.OnNavigatedTo(object?)"/>；
/// <c>BitmapImage(Uri)</c> → <see cref="Bitmap(string)"/>；<c>User32.SetForegroundWindow</c> → <c>Window.Activate()</c>；
/// <c>InfoBar</c> 进度条 + 取消按钮 → 内联 <see cref="ProgressToast"/>（复用 InAppToast 容器）；
/// <c>ContentDialog</c> → <see cref="DialogService"/><c>+StarwardContentDialog</c>；
/// <c>Launcher.LaunchFolderAsync(FolderLauncherOptions)</c> → <see cref="RevealInFileManager"/>（xdg-open）；
/// <c>StorageProvider</c> 文件选择 → <see cref="FileDialogHelper"/>；
/// <c>Visibility</c> → <see cref="IsVisibleProperty"/>；<c>[INotifyPropertyChanged]</c>/<c>[ObservableProperty]</c> → 手写属性。
/// </remarks>
public sealed partial class GachaLogPage : PageBase, INotifyPropertyChanged
{

    private readonly ILogger<GachaLogPage> _logger = AppConfig.GetLogger<GachaLogPage>();

    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();

    private readonly GenshinBeyondGachaService _genshinBeyondGachaService = AppConfig.GetService<GenshinBeyondGachaService>();


    private GachaLogService _gachaLogService;


    public GachaLogPage()
    {
        this.InitializeComponent();
        DataContext = this;
    }



    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        GachaTypeText = GachaLogService.GetGachaLogText(CurrentGameBiz);
        // 旧版导出对应的标准：原神是 UIGF v3.0，星铁是 SRGF v1.0，
        // 绝区零没有对应标准，Starward 只是套用了同样的结构，故仍标为 JSON
        LegacyFormatName = CurrentGameBiz.Game switch
        {
            GameBiz.hk4e => "UIGF v3.0",
            GameBiz.hkrpg => "SRGF v1.0",
            _ => "JSON",
        };
        LegacyImportText = string.Format(Lang.GachaLogPage_ImportFrom0, LegacyFormatName);
        if (CurrentGameBiz.Game == GameBiz.hk4e)
        {
            EnableGenshinGachaItemStats = true;
            _gachaLogService = AppConfig.GetService<GenshinGachaService>();
            Image_Emoji.Source = new Bitmap(AppConfig.EmojiPaimon.LocalPath);
        }
        if (CurrentGameBiz.Game == GameBiz.hkrpg)
        {
            EnableStarRailGachaItemStats = true;
            _gachaLogService = AppConfig.GetService<StarRailGachaService>();
            Image_Emoji.Source = new Bitmap(AppConfig.EmojiPom.LocalPath);
        }
        if (CurrentGameBiz.Game == GameBiz.nap)
        {
            EnableZZZGachaItemStats = true;
            IsZZZGachaStatsCardVisible = true;
            _gachaLogService = AppConfig.GetService<ZZZGachaService>();
            Image_Emoji.Source = new Bitmap(AppConfig.EmojiBangboo.LocalPath);
            MenuFlyoutItem_CloudGame.IsVisible = false;
            if (CurrentGameBiz.Value is GameBiz.nap_cn or GameBiz.nap_bilibili)
            {
                MenuFlyoutItem_SyncFromMiyoushe.IsVisible = true;
                MenuFlyoutItem_SyncFromMiyousheAll.IsVisible = true;
            }
            if (CurrentGameBiz == GameBiz.nap_global)
            {
                MenuFlyoutItem_SyncFromHoYoLAB.IsVisible = true;
                MenuFlyoutItem_SyncFromHoYoLABAll.IsVisible = true;
            }
        }
        if (CurrentGameBiz.IsGlobalServer())
        {
            MenuFlyoutItem_CloudGame.IsVisible = false;
        }
    }


    private bool _isZZZGachaStatsCardVisible;

    public bool IsZZZGachaStatsCardVisible
    {
        get => _isZZZGachaStatsCardVisible;
        set => SetProperty(ref _isZZZGachaStatsCardVisible, value);
    }


    private string _gachaTypeText;

    public string GachaTypeText
    {
        get => _gachaTypeText;
        set => SetProperty(ref _gachaTypeText, value);
    }


    private string _legacyFormatName = "JSON";

    /// <summary>
    /// 旧版（非 UIGF v4.x）导出格式名称，随游戏变化
    /// </summary>
    public string LegacyFormatName
    {
        get => _legacyFormatName;
        set => SetProperty(ref _legacyFormatName, value);
    }


    private string _legacyImportText = "";

    /// <summary>
    /// 旧版导入按钮文字，如「从 UIGF v3.0 导入」
    /// </summary>
    public string LegacyImportText
    {
        get => _legacyImportText;
        set => SetProperty(ref _legacyImportText, value);
    }


    private ObservableCollection<long> _uidList;

    public ObservableCollection<long> UidList
    {
        get => _uidList;
        set => SetProperty(ref _uidList, value);
    }


    private long? _selectUid;

    public long? SelectUid
    {
        get => _selectUid;
        set
        {
            if (SetProperty(ref _selectUid, value))
            {
                AppConfig.SetLastUidInGachaLogPage(CurrentGameBiz.Game, value ?? 0);
                UpdateGachaTypeStats(value);
            }
        }
    }




    protected override async void OnLoaded()
    {
        await Task.Delay(16);
        WeakReferenceMessenger.Default.Register<UpdateGachaLogMessage>(this, (s, m) =>
        {
            if (m.GameBiz == CurrentGameBiz)
            {
                (TopLevel.GetTopLevel(this) as Window)?.Activate();
                _ = UpdateGachaLogInternalAsync(m.Url);
            }
        });
        Grid_GachaStats.PointerWheelChanged += Grid_GachaStats_PointerWheelChanged;
        Initialize();
        await UpdateWikiDataAsync();
    }



    protected override void OnUnloaded()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        Grid_GachaStats.PointerWheelChanged -= Grid_GachaStats_PointerWheelChanged;
        if (DisplayGachaTypeStatsCollection is not null)
        {
            DisplayGachaTypeStatsCollection.Clear();
            DisplayGachaTypeStatsCollection = null!;
        }
        if (GachaItemStats is not null)
        {
            GachaItemStats.Clear();
            GachaItemStats = null;
        }
        ListView_GachaBanners.SelectionChanged -= ListView_GachaBanners_SelectionChanged;
        if (GachaBanners is not null)
        {
            GachaBanners.Clear();
            GachaBanners = null!;
        }
    }



    private void Initialize()
    {
        try
        {
            InitializeGachaBanners();
            SelectUid = null;
            UidList = new(_gachaLogService.GetUids());
            var lastUid = AppConfig.GetLastUidInGachaLogPage(CurrentGameBiz.Game);
            if (UidList.Contains(lastUid))
            {
                SelectUid = lastUid;
            }
            else
            {
                SelectUid = UidList.FirstOrDefault();
            }
            if (UidList.Count == 0)
            {
                StackPanel_Emoji.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize");
        }
    }



    private void Grid_GachaStats_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (Math.Abs(e.Delta.X) > 0.001 || shift)
        {
            double delta = Math.Abs(e.Delta.X) > 0.001 ? e.Delta.X : e.Delta.Y;
            var offset = ScrollViewer_GachaStats.Offset;
            ScrollViewer_GachaStats.Offset = new Vector(offset.X - delta * 50, offset.Y);
            e.Handled = true;
        }
    }



    private async Task UpdateWikiDataAsync()
    {
        try
        {
            string lang = string.IsNullOrWhiteSpace(GachaLanguage) ? System.Globalization.CultureInfo.CurrentUICulture.Name : GachaLanguage;
            await _gachaLogService.UpdateGachaInfoAsync(CurrentGameBiz, lang);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update wiki data {gameBiz}", CurrentGameBiz);
        }
    }




    [RelayCommand]
    private void OpenItemStatsPane()
    {
        SplitView_Content.IsPaneOpen = true;
    }




    #region Gacha Stats



    private bool _enableGenshinGachaItemStats;

    public bool EnableGenshinGachaItemStats
    {
        get => _enableGenshinGachaItemStats;
        set => SetProperty(ref _enableGenshinGachaItemStats, value);
    }


    private bool _enableStarRailGachaItemStats;

    public bool EnableStarRailGachaItemStats
    {
        get => _enableStarRailGachaItemStats;
        set => SetProperty(ref _enableStarRailGachaItemStats, value);
    }


    private bool _enableZZZGachaItemStats;

    public bool EnableZZZGachaItemStats
    {
        get => _enableZZZGachaItemStats;
        set => SetProperty(ref _enableZZZGachaItemStats, value);
    }


    private List<GachaBanner> _gachaBanners;

    public List<GachaBanner> GachaBanners
    {
        get => _gachaBanners;
        set => SetProperty(ref _gachaBanners, value);
    }


    private ObservableCollection<GachaTypeStats> _displayGachaTypeStatsCollection;

    public ObservableCollection<GachaTypeStats> DisplayGachaTypeStatsCollection
    {
        get => _displayGachaTypeStatsCollection;
        set => SetProperty(ref _displayGachaTypeStatsCollection, value);
    }


    private List<GachaLogItemEx>? _gachaItemStats;

    public List<GachaLogItemEx>? GachaItemStats
    {
        get => _gachaItemStats;
        set => SetProperty(ref _gachaItemStats, value);
    }


    private List<GachaTypeStats>? gachaTypeStats;


    private int errorCount = 0;


    private void InitializeGachaBanners()
    {
        GachaBanners = _gachaLogService.QueryGachaTypes.Select(x => new GachaBanner(x)).ToList();
        string? banner = AppConfig.GetDisplayGachaBanners(CurrentGameBiz.Game);
        if (!string.IsNullOrWhiteSpace(banner))
        {
            foreach (var item in banner.Split(','))
            {
                if (int.TryParse(item, out int type))
                {
                    if (GachaBanners.FirstOrDefault(x => x.Value == type) is GachaBanner gachaType)
                    {
                        ListView_GachaBanners.SelectedItems.Add(gachaType);
                    }
                }
            }
        }
        if (ListView_GachaBanners.SelectedItems.Count == 0)
        {
            foreach (var item in GachaBanners)
            {
                ListView_GachaBanners.SelectedItems.Add(item);
            }
        }
        if (CurrentGameBiz.Game is GameBiz.hkrpg && !AppConfig.GetValue(false, "SavedStarRailBannersAfterCollaborationStarting"))
        {
            if (ListView_GachaBanners.SelectedItems.Cast<GachaBanner>().FirstOrDefault(x => x.Value == 21) is null)
            {
                ListView_GachaBanners.SelectedItems.Add(GachaBanners.FirstOrDefault(x => x.Value == 21));
            }
            if (ListView_GachaBanners.SelectedItems.Cast<GachaBanner>().FirstOrDefault(x => x.Value == 22) is null)
            {
                ListView_GachaBanners.SelectedItems.Add(GachaBanners.FirstOrDefault(x => x.Value == 22));
            }
        }
        if (CurrentGameBiz.Game is GameBiz.nap && !AppConfig.GetValue(false, "SavedZZZBannersSinceVersion2"))
        {
            if (ListView_GachaBanners.SelectedItems.Cast<GachaBanner>().FirstOrDefault(x => x.Value == 102) is null)
            {
                ListView_GachaBanners.SelectedItems.Add(GachaBanners.FirstOrDefault(x => x.Value == 102));
            }
            if (ListView_GachaBanners.SelectedItems.Cast<GachaBanner>().FirstOrDefault(x => x.Value == 103) is null)
            {
                ListView_GachaBanners.SelectedItems.Add(GachaBanners.FirstOrDefault(x => x.Value == 103));
            }
        }
        ListView_GachaBanners.SelectionChanged -= ListView_GachaBanners_SelectionChanged;
        ListView_GachaBanners.SelectionChanged += ListView_GachaBanners_SelectionChanged;
    }


    private void ListView_GachaBanners_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            string value = string.Join(',', ListView_GachaBanners.SelectedItems.Cast<GachaBanner>().Select(x => x.Value));
            AppConfig.SetDisplayGachaBanners(CurrentGameBiz.Game, value);
            if (CurrentGameBiz.Game is GameBiz.hkrpg)
            {
                AppConfig.SetValue(true, "SavedStarRailBannersAfterCollaborationStarting");
            }
            if (CurrentGameBiz.Game is GameBiz.nap)
            {
                AppConfig.SetValue(true, "SavedZZZBannersSinceVersion2.5");
            }
            UpdateDisplayGachaTypeStats();
        }
        catch { }
    }



    private void UpdateGachaTypeStats(long? uid)
    {
        try
        {
            if (uid is null or 0)
            {
                gachaTypeStats = null;
                DisplayGachaTypeStatsCollection = [];
                GachaItemStats = null;
                StackPanel_Emoji.IsVisible = true;
            }
            else
            {
                (gachaTypeStats, GachaItemStats) = _gachaLogService.GetGachaTypeStats(uid.Value);
                UpdateDisplayGachaTypeStats();
                StackPanel_Emoji.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateGachaTypeStats");
        }
    }


    private void UpdateDisplayGachaTypeStats()
    {
        if (gachaTypeStats is null)
        {
            return;
        }
        DisplayGachaTypeStatsCollection ??= [];
        DisplayGachaTypeStatsCollection.Clear();
        var list = ListView_GachaBanners.SelectedItems.Cast<GachaBanner>().ToList();
        if (list.Count == 0)
        {
            list = GachaBanners;
        }
        foreach (var item in list)
        {
            if (gachaTypeStats.FirstOrDefault(x => x.GachaType == item.Value) is GachaTypeStats stats)
            {
                DisplayGachaTypeStatsCollection.Add(stats);
            }
        }
    }


    private void UpdateGachaStatsCardLayout()
    {
        try
        {
            if (ItemsControl_GachaStats != null)
            {
                int count = ItemsControl_GachaStats.ItemCount;
                if (count > 0)
                {
                    double width = (ScrollViewer_GachaStats.Bounds.Width - 40 - (count - 1) * 12) / count;
                    width = Math.Clamp(width, 262, double.MaxValue);
                    for (int i = 0; i < count; i++)
                    {
                        var a = ItemsControl_GachaStats.ContainerFromIndex(i);
                        if (a is ContentPresenter presenter)
                        {
                            presenter.Width = width;
                        }
                    }
                }
            }
            if (ItemsControl_ZZZGachaStats != null)
            {
                int count = ItemsControl_ZZZGachaStats.ItemCount;
                if (count > 0)
                {
                    double width = (ScrollViewer_GachaStats.Bounds.Width - 40 - (count - 1) * 12) / count;
                    width = Math.Clamp(width, 262, double.MaxValue);
                    for (int i = 0; i < count; i++)
                    {
                        if (ItemsControl_ZZZGachaStats.ContainerFromIndex(i) is ContentPresenter presenter)
                        {
                            presenter.Width = width;
                        }
                    }
                }
            }
        }
        catch { }
    }


    private void GachaStatsCard_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateGachaStatsCardLayout();
    }


    private void GachaStatsCard_Unloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateGachaStatsCardLayout();
    }



    #endregion



    #region Get Gacha



    [RelayCommand]
    private async Task UpdateGachaLogAsync(string? param = null)
    {
        try
        {
            string? url = null;
            if (param is "cache")
            {
                if (SelectUid is null or 0)
                {
                    return;
                }
                url = _gachaLogService.GetGachaLogUrlByUid(SelectUid.Value);
                if (string.IsNullOrWhiteSpace(url))
                {
                    // 无法找到 uid {uid} 的已缓存 URL
                    InAppToast.MainWindow?.Warning(null, string.Format(Lang.GachaLogPage_CannotFindSavedURLOfUid, SelectUid));
                    return;
                }
            }
            else
            {
                var path = GameLauncherService.GetGameInstallPath(CurrentGameId);
                if (!Directory.Exists(path))
                {
                    // 游戏未安装
                    InAppToast.MainWindow?.Warning(null, Lang.GachaLogPage_GameNotInstalled);
                    return;
                }
                url = _gachaLogService.GetGachaLogUrlFromWebCache(CurrentGameBiz, path);
                if (string.IsNullOrWhiteSpace(url))
                {
                    // 无法找到 URL，请在游戏中打开抽卡记录页面
                    errorCount++;
                    if (errorCount > 2 && IsGachaCacheFileExists())
                    {
                        errorCount = 0;
                        InAppToast.MainWindow?.ShowWithButton(InfoBarSeverity.Warning,
                                                              Lang.GachaLogPage_AlwaysFailedToGetGachaRecords,
                                                              Lang.GachaLogPage_RestartGameAfterDeletingTheCacheFolder,
                                                              Lang.GachaLogPage_DeleteCacheFolder,
                                                              () => _ = DeleteGachaCacheFolderAsync());
                    }
                    else
                    {
                        InAppToast.MainWindow?.Warning(null, Lang.GachaLogPage_CannotFindURL);
                    }
                    return;
                }
            }
            await UpdateGachaLogInternalAsync(url, param is "all");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update gacha log");
            InAppToast.MainWindow?.Error(ex);
        }
    }




    private async Task UpdateGachaLogInternalAsync(string url, bool all = false)
    {
        try
        {
            var uid = await _gachaLogService.GetUidFromGachaLogUrl(url);
            var cancelSource = new CancellationTokenSource();
            GachaProgressToast? toast = ShowProgressToast(cancelSource);
            var progress = new Progress<string>((str) =>
            {
                if (toast is not null)
                {
                    toast.Message = str;
                }
            });
            var newUid = await _gachaLogService.GetGachaLogAsync(url, all, GachaLanguage, progress, cancelSource.Token);
            CloseProgressToast(toast, $"Uid {newUid}", InfoBarSeverity.Success);
            if (SelectUid == uid)
            {
                UpdateGachaTypeStats(uid);
            }
            else
            {
                if (!UidList.Contains(uid))
                {
                    UidList.Add(uid);
                }
                SelectUid = uid;
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Get gacha log canceled");
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogWarning("Request mihoyo api error: {error}", ex.Message);
            // 原铁 -101 绝 -1
            if (ex.ReturnCode is -101 or -1)
            {
                // authkey timeout
                // 请在游戏中打开抽卡记录页面后再重试
                errorCount++;
                if (errorCount > 1 && IsGachaCacheFileExists())
                {
                    errorCount = 0;
                    InAppToast.MainWindow?.ShowWithButton(InfoBarSeverity.Warning,
                                                          Lang.GachaLogPage_AlwaysFailedToGetGachaRecords,
                                                          Lang.GachaLogPage_RestartGameAfterDeletingTheCacheFolder,
                                                          Lang.GachaLogPage_DeleteCacheFolder,
                                                          () => _ = DeleteGachaCacheFolderAsync());
                }
                else
                {
                    InAppToast.MainWindow?.Warning("Authkey Timeout", Lang.GachaLogPage_PleaseOpenTheGachaRecordsPageInGameAndTryAgain);
                }
            }
            else
            {
                InAppToast.MainWindow?.Warning(null, ex.Message);
            }
        }
    }


    [RelayCommand]
    private async Task SyncFromMiyousheAsync(string? param = null)
    {
        try
        {
            if (CurrentGameBiz.Game != GameBiz.nap)
            {
                InAppToast.MainWindow?.Warning(null, Lang.GachaLogPage_OnlySupportZZZCNServer);
                return;
            }
            if (_gachaLogService is not ZZZGachaService zzzGachaService)
            {
                throw new InvalidOperationException($"Current gacha service type is {_gachaLogService.GetType().Name}.");
            }
            GameBiz roleGameBiz = CurrentGameBiz == GameBiz.nap_bilibili ? GameBiz.nap_cn : CurrentGameBiz;
            var roles = _gameRecordService.GetGameRoles(roleGameBiz);
            if (roles.Count == 0)
            {
                InAppToast.MainWindow?.Warning(null, Lang.GachaLogPage_PleaseLoginMiyousheAndAddZZZRole);
                WeakReferenceMessenger.Default.Send(new MainViewNavigateMessage(typeof(GameRecordPage)));
                return;
            }
            GameRecordRole? role;
            if (roles.Count == 1)
            {
                role = roles[0];
            }
            else
            {
                role = await SelectMiyousheRoleAsync(roles, roleGameBiz);
                if (role is null)
                {
                    return;
                }
            }
            if (string.IsNullOrWhiteSpace(role.Cookie))
            {
                InAppToast.MainWindow?.Warning(null, Lang.GachaLogPage_CurrentAccountMissingCookiePleaseReloginMiyoushe);
                return;
            }
            _gameRecordService.SetLastSelectGachaSyncRole(roleGameBiz, role);
            var cancelSource = new CancellationTokenSource();
            GachaProgressToast? toast = ShowProgressToast(cancelSource);
            var progress = new Progress<string>((str) =>
            {
                if (toast is not null)
                {
                    toast.Message = str;
                }
            });
            var uid = await zzzGachaService.GetGachaLogByGameRecordAsync(role, param is "all", GachaLanguage, progress, cancelSource.Token);
            CloseProgressToast(toast, $"Uid {uid}", InfoBarSeverity.Success);
            if (SelectUid == uid)
            {
                UpdateGachaTypeStats(uid);
            }
            else
            {
                if (!UidList.Contains(uid))
                {
                    UidList.Add(uid);
                }
                SelectUid = uid;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Sync zzz gacha record from miyoushe canceled");
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogWarning(ex, "Sync zzz gacha record from miyoushe error ({retcode})", ex.ReturnCode);
            InAppToast.MainWindow?.Warning(null, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Sync zzz gacha record from miyoushe is not supported");
            InAppToast.MainWindow?.Warning(null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync zzz gacha record from miyoushe");
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private async Task<GameRecordRole?> SelectMiyousheRoleAsync(List<GameRecordRole> roles, GameBiz roleGameBiz)
    {
        var items = roles.Select(role => new MiyousheRoleItem(role)).ToList();
        MiyousheRoleItem? selectedItem = null;
        if (SelectUid is long currentUid && currentUid > 0)
        {
            selectedItem = items.FirstOrDefault(x => x.Role.Uid == currentUid);
        }
        var selectedRole = _gameRecordService.GetLastSelectGachaSyncRoleOrTheFirstOne(roleGameBiz);
        selectedItem ??= items.FirstOrDefault(x =>
            selectedRole is not null
            && x.Role.Uid == selectedRole.Uid);
        var comboBox = new ComboBox
        {
            MinWidth = 420,
            ItemsSource = items,
            DisplayMemberBinding = new Avalonia.Data.Binding(nameof(MiyousheRoleItem.DisplayText)),
            SelectedItem = selectedItem ?? items.FirstOrDefault(),
        };
        var panel = new StackPanel
        {
            Spacing = 8,
        };
        panel.Children.Add(new TextBlock
        {
            Text = Lang.GachaLogPage_SelectMiyousheRoleDescription,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(comboBox);
        bool primary = false;
        var dialog = new StarwardContentDialog
        {
            Title = Lang.GachaLogPage_SelectMiyousheRole,
            Content = panel,
            PrimaryButtonText = Lang.Common_Confirm,
            SecondaryButtonText = Lang.Common_Cancel,
            IsPrimaryButtonEnabled = comboBox.SelectedItem is not null,
        };
        comboBox.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = comboBox.SelectedItem is not null;
        dialog.PrimaryButtonClicked += (_, _) => primary = true;
        await DialogService.ShowAsync(dialog, this);
        if (!primary)
        {
            return null;
        }
        if (comboBox.SelectedItem is not MiyousheRoleItem item)
        {
            InAppToast.MainWindow?.Warning(null, Lang.GachaLogPage_PleaseSelectMiyousheRole);
            return null;
        }
        return item.Role;
    }


    private sealed class MiyousheRoleItem
    {
        public GameRecordRole Role { get; }

        public string DisplayText { get; }

        public MiyousheRoleItem(GameRecordRole role)
        {
            Role = role;
            DisplayText = $"{role.Nickname} ({role.Uid}) | {role.RegionName}";
        }
    }



    [RelayCommand]
    private async Task InputUrlAsync()
    {
        try
        {
            var textbox = new TextBox();
            bool primary = false;
            var dialog = new StarwardContentDialog
            {
                // 输入 URL
                Title = Lang.GachaLogPage_InputURL,
                Content = textbox,
                // 确认
                PrimaryButtonText = Lang.Common_Confirm,
                // 取消
                SecondaryButtonText = Lang.Common_Cancel,
            };
            dialog.PrimaryButtonClicked += (_, _) => primary = true;
            await DialogService.ShowAsync(dialog, this);
            if (primary)
            {
                var url = textbox.Text;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    await UpdateGachaLogInternalAsync(url);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Input url");
            InAppToast.MainWindow?.Error(ex);
        }
    }




    [RelayCommand]
    private void OpenCloudGameWindow()
    {
        try
        {
            new CloudGameGachaWindow { GameBiz = CurrentGameBiz }.Show();
        }
        catch { }
    }



    #endregion



    #region Gacha Setting Panel


    private string? _gachaLanguage = AppConfig.GachaLanguage;

    public string? GachaLanguage
    {
        get => _gachaLanguage;
        set
        {
            if (SetProperty(ref _gachaLanguage, value))
            {
                AppConfig.GachaLanguage = value;
            }
        }
    }



    [RelayCommand]
    private async Task CopyUrlAsync()
    {
        try
        {
            if (SelectUid is null or 0)
            {
                return;
            }
            var url = _gachaLogService.GetGachaLogUrlByUid(SelectUid.Value);
            if (!string.IsNullOrWhiteSpace(url))
            {
                ClipboardHelper.SetText(url);
                TextBlock_CopyUrl.Text = "\uE8FB"; // accept
                await Task.Delay(1000);
                TextBlock_CopyUrl.Text = "\uE8C8";  // copy
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy url");
        }
    }



    [RelayCommand]
    private async Task ChangeGachaItemNameAsync()
    {
        try
        {
            string lang = string.IsNullOrWhiteSpace(GachaLanguage) ? System.Globalization.CultureInfo.CurrentUICulture.Name : GachaLanguage;
            (lang, int count) = await _gachaLogService.ChangeGachaItemNameAsync(CurrentGameBiz, lang);
            InAppToast.MainWindow?.Success(null, string.Format(Lang.GachaLogPage_0GachaItemsHaveBeenChangedToLanguage1, count, lang), 5000);
            UpdateGachaTypeStats(SelectUid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change gacha item name");
        }
    }



    [RelayCommand]
    private async Task DeleteUidAsync()
    {
        try
        {
            var uid = SelectUid;
            if (uid is null or 0)
            {
                return;
            }
            bool primary = false;
            var dialog = new StarwardContentDialog
            {
                // 警告
                Title = Lang.Common_Warning,
                // 即将删除 Uid {uid} 的所有抽卡记录，此操作不可恢复。
                Content = new TextBlock
                {
                    Text = string.Format(Lang.GachaLogPage_DeleteGachaRecordsWarning, uid),
                    TextWrapping = TextWrapping.Wrap,
                },
                // 删除
                PrimaryButtonText = Lang.Common_Delete,
                // 取消
                SecondaryButtonText = Lang.Common_Cancel,
            };
            dialog.PrimaryButtonClicked += (_, _) => primary = true;
            await DialogService.ShowAsync(dialog, this);
            if (primary)
            {
                var count = _gachaLogService.DeleteUid(uid.Value);
                // 已删除 Uid {uid} 的抽卡记录 {count} 条
                InAppToast.MainWindow?.Success(null, string.Format(Lang.GachaLogPage_DeletedGachaRecordsOfUid, count, uid));
                Initialize();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete uid");
            InAppToast.MainWindow?.Error(ex);
        }
    }



    [RelayCommand]
    private async Task DeleteUidByTimeAsync()
    {
        try
        {
            var dialog = new DeleteGachaLogDialog
            {
                CurrentGameBiz = this.CurrentGameBiz,
                DefaultUid = this.SelectUid,
            };
            await DialogService.ShowAsync(dialog, this);
            if (dialog.Deleted)
            {
                UpdateGachaTypeStats(dialog.SelectUid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete uid");
        }
    }



    [RelayCommand]
    private async Task DeleteGachaCacheFolderAsync()
    {
        try
        {
            var installPath = GameLauncherService.GetGameInstallPath(CurrentGameId);
            if (!string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath))
            {
                var webCachesPath = GachaLogClient.GetWebCachesFolderPath(CurrentGameBiz, installPath);
                if (Directory.Exists(webCachesPath))
                {
                    var folder = Directory.GetParent(webCachesPath);
                    await Task.Run(() => RevealInFileManager(webCachesPath, folder?.FullName));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete gacha cache file");
        }
    }



    private bool IsGachaCacheFileExists()
    {
        try
        {
            var installPath = GameLauncherService.GetGameInstallPath(CurrentGameId);
            if (!string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath))
            {
                var path = GachaLogClient.GetGachaCacheFilePath(CurrentGameBiz, installPath);
                return File.Exists(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check gacha cache file exists");
        }
        return false;
    }



    #endregion



    #region Import & Export



    [RelayCommand]
    private async Task ExportGachaLogAsync(string format)
    {
        try
        {
            if (SelectUid is null or 0)
            {
                return;
            }
            long uid = SelectUid.Value;
            if (!await EnsureExportWithoutGenshinBeyondAsync(uid))
            {
                return;
            }
            var ext = format switch
            {
                "excel" => "xlsx",
                "json" => "json",
                _ => "json"
            };
            var suggestName = $"Starward_Export_{CurrentGameBiz.Game}_{uid}_{DateTime.Now:yyyyMMddHHmmss}.{ext}";
            var file = await FileDialogHelper.OpenSaveFileDialogAsync(this, suggestName, (ext, $".{ext}"));
            if (file is not null)
            {
                await _gachaLogService.ExportGachaLogAsync(uid, file, format);
                await Task.Run(() => RevealInFileManager(file, Path.GetDirectoryName(file)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export gacha log");
            InAppToast.MainWindow?.Error(ex);
        }
    }




    /// <summary>
    /// UIGF v3.0 不支持千星奇域。若当前 Uid 存在千星奇域记录，提示用户这部分记录不会被导出，
    /// 返回 false 表示用户选择取消，中断本次导出。
    /// </summary>
    private async Task<bool> EnsureExportWithoutGenshinBeyondAsync(long uid)
    {
        if (CurrentGameBiz.Game != GameBiz.hk4e)
        {
            return true;
        }
        if (!_genshinBeyondGachaService.GetUids().Contains(uid))
        {
            return true;
        }
        bool primary = false;
        var dialog = new StarwardContentDialog
        {
            Title = Lang.GachaLogPage_UIGF30DoesNotSupportMiliastraWonderlandOde,
            Content = new TextBlock
            {
                Text = Lang.GachaLogPage_UIGF30DoesNotSupportMiliastraWonderlandOdeDesc,
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = Lang.Common_Continue,
            SecondaryButtonText = Lang.Common_Cancel,
        };
        dialog.PrimaryButtonClicked += (_, _) => primary = true;
        await DialogService.ShowAsync(dialog, this);
        return primary;
    }




    [RelayCommand]
    private async Task ImportGachaLogAsync()
    {
        try
        {
            var file = await FileDialogHelper.PickSingleFileAsync(this, ("Json", ".json"));
            if (file is not null && File.Exists(file))
            {
                var uid = _gachaLogService.ImportGachaLog(file);
                if (uid == SelectUid)
                {
                    UpdateGachaTypeStats(uid);
                }
                else if (UidList.Contains(uid))
                {
                    SelectUid = uid;
                }
                else
                {
                    UidList.Add(uid);
                    SelectUid = uid;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import gacha log");
            InAppToast.MainWindow?.Error(ex);
        }
    }



    [RelayCommand]
    private void OpenUIGF4Window()
    {
        new UIGF4GachaWindow().Show();
    }







    #endregion



    #region Platform


    /// <summary>
    /// 上游 <c>Launcher.LaunchFolderAsync(folder, new FolderLauncherOptions { ItemsToSelect = ... })</c>：
    /// 打开文件管理器并选中目标。Linux 下用 <c>xdg-open</c> 打开父目录。
    /// </summary>
    private static void RevealInFileManager(string path, string? parent)
    {
        try
        {
            string folder = parent ?? Path.GetDirectoryName(path) ?? path;
            if (!Directory.Exists(folder))
            {
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                ArgumentList = { folder },
                UseShellExecute = false,
            });
        }
        catch
        {
        }
    }


    private GachaProgressToast? ShowProgressToast(CancellationTokenSource cancelSource)
    {
        return GachaProgressToast.Show(cancelSource);
    }


    private void CloseProgressToast(GachaProgressToast? toast, string? title, InfoBarSeverity severity)
    {
        GachaProgressToast.Close(toast, title, severity);
    }

    #endregion




    #region INotifyPropertyChanged

    /// <summary>
    /// Avalonia 的 <see cref="AvaloniaObject"/> 已实现 <see cref="INotifyPropertyChanged"/>，但只对其
    /// AvaloniaProperty 发通知；本页的 CLR 属性走这里。用 <c>new</c> 隐藏基类事件，并把接口分发显式重映射过来。
    /// </summary>
    public new event PropertyChangedEventHandler? PropertyChanged;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropertyChanged += value;
        remove => PropertyChanged -= value;
    }


    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    #endregion


}
