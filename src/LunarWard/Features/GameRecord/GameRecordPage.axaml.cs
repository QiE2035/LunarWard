using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.GameRecord.Genshin;
using LunarWard.Features.GameRecord.StarRail;
using LunarWard.Features.GameRecord.ZZZ;
using LunarWard.Features.ViewHost;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using LunarWard.Services;
using LunarWard.Views.Starward.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;


namespace LunarWard.Features.GameRecord;

public sealed partial class GameRecordPage : PageBase
{


    private readonly ILogger<GameRecordPage> _logger = AppConfig.GetLogger<GameRecordPage>();


    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public GameRecordPage()
    {
        this.InitializeComponent();
    }


    public override void OnNavigatedTo(object? e)
    {
        base.OnNavigatedTo(e);
        CurrentGameBiz = CurrentGameBiz.Value switch
        {
            GameBiz.hk4e_bilibili => GameBiz.hk4e_cn,
            GameBiz.hkrpg_bilibili => GameBiz.hkrpg_cn,
            GameBiz.nap_bilibili => GameBiz.nap_cn,
            _ => CurrentGameBiz,
        };
        _gameRecordService.IsHoyolab = CurrentGameBiz.IsGlobalServer();
        if (CurrentGameBiz.IsGlobalServer())
        {
            NavigationViewItem_UpdateDeviceInfo.IsVisible = false;
        }
        _gameRecordService.Language = System.Globalization.CultureInfo.CurrentUICulture.Name;
        InitializeNavigationViewItemVisibility();
    }


    protected override async void OnLoaded()
    {
        if (AppConfig.HoyolabToolboxPaneOpen)
        {
            OpenNavigationViewPane();
        }
        else
        {
            CloseNavigationViewPane();
        }
        WeakReferenceMessenger.Default.Register<GameRecordRoleChangedMessage>(this, (r, m) =>
        {
            LoadGameRoles(m.GameRole);
        });
        WeakReferenceMessenger.Default.Register<GameRecordVerifyAccountMessage>(this, (r, m) =>
        {
            ShowBattleChronicleWindow();
        });
        await Task.Delay(16);
        NavigateTo(typeof(BlankPage));
        if (await CheckAgreementAsync())
        {
            LoadGameRoles();
            await UpdateDeviceInfoAsync();
            await RefreshGameRoleHeadIconSilentlyAsync();
        }
    }


    protected override void OnUnloaded()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        NavigationViewItem_BattleChronicle.Click -= NavigationViewItem_BattleChronicle_Click;
        NavigationViewItem_UpdateDeviceInfo.Click -= NavigationViewItem_UpdateDeviceInfo_Click;
        CurrentRole = null;
        GameRoleList = null!;
        _battleChronicleWindow = null;
    }


    private async Task<bool> CheckAgreementAsync()
    {
        try
        {
            if (!AppConfig.AcceptHoyolabToolboxAgreement)
            {
                // Linux: 上游 WinUI ContentDialog（ShowAsync + 5 秒倒计时禁用主按钮）：
                // 这里用 StarwardContentDialog + DialogService（本仓库的 ContentDialog 等价物）。
                // 倒计时期间点「拒绝」会关闭对话框，ShowAsync 随之返回，accepted 保持 false。
                var dialog = new StarwardContentDialog
                {
                    Title = Lang.Common_Disclaimer,
                    Content = new TextBlock
                    {
                        Text = Lang.HoyolabToolboxPage_DisclaimerContent,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    PrimaryButtonText = Lang.Common_Accept + " (5s)",
                    SecondaryButtonText = Lang.Common_Reject,
                    IsPrimaryButtonEnabled = false,
                };
                bool accepted = false;
                dialog.PrimaryButtonClicked += (_, _) => accepted = true;
                Task showTask = DialogService.ShowAsync(dialog, this);
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(1000);
                    if (!DialogService.IsDialogOpen)
                    {
                        break;
                    }
                    dialog.PrimaryButtonText = Lang.Common_Accept + $" ({4 - i}s)";
                }
                dialog.PrimaryButtonText = Lang.Common_Accept;
                dialog.IsPrimaryButtonEnabled = true;
                await showTask;
                if (accepted)
                {
                    AppConfig.AcceptHoyolabToolboxAgreement = true;
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(new MainViewNavigateMessage(typeof(GameLauncherPage)));
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check agreement.");
            return false;
        }
    }


    #region Navigation Style


    public Thickness NavigationViewItemContentMargin { get; set => SetProperty(ref field, value); } = new Thickness(-2, 0, 0, 0);


    // Close pane
    private void Grid_Avatar_1_Tapped(object? sender, TappedEventArgs e)
    {
        CloseNavigationViewPane();
    }


    // Open pane
    private void Border_Avatar_2_Tapped(object? sender, TappedEventArgs e)
    {
        OpenNavigationViewPane();
    }


    private void OpenNavigationViewPane()
    {
        NavigationViewItemContentMargin = new Thickness(-2, 0, 0, 0);
        NavigationView_Toolbox.IsPaneOpen = true;
        Grid_Avatar_1.IsVisible = true;
        Border_Avatar_2.IsVisible = false;
        AppConfig.HoyolabToolboxPaneOpen = true;
    }


    private void CloseNavigationViewPane()
    {
        NavigationViewItemContentMargin = new Thickness(2, 0, 0, 0);
        NavigationView_Toolbox.IsPaneOpen = false;
        Grid_Avatar_1.IsVisible = false;
        Border_Avatar_2.IsVisible = true;
        AppConfig.HoyolabToolboxPaneOpen = false;
    }


    private void InitializeNavigationViewItemVisibility()
    {
        if (CurrentGameBiz.Game is GameBiz.bh3)
        {
            NavigationViewItem_BattleChronicle.IsVisible = true;
            // 崩坏3战绩图片
            Image_BattleChronicle.Source = GameRecordAssets.FromMsAppx("Assets/Image/4d94fbd5ff63c8b4344876ce21e04d10_2581928258151711511.png");
        }
        else if (CurrentGameBiz.Game is GameBiz.hk4e)
        {
            NavigationViewItem_BattleChronicle.IsVisible = true;
            NavigationViewItem_TravelersDiary.IsVisible = true;
            NavigationViewItem_SpiralAbyss.IsVisible = true;
            NavigationViewItem_ImaginariumTheater.IsVisible = true;
            NavigationViewItem_StygianOnslaught.IsVisible = true;
            // 原神战绩图片
            Image_BattleChronicle.Source = GameRecordAssets.FromMsAppx("Assets/Image/ced4deac2162690105bbc8baad2b51a3_4109616186965788891.png");
        }
        else if (CurrentGameBiz.Game is GameBiz.hkrpg)
        {
            NavigationViewItem_BattleChronicle.IsVisible = true;
            NavigationViewItem_TrailblazeMonthlyCalendar.IsVisible = true;
            NavigationViewItem_SimulatedUniverse.IsVisible = true;
            NavigationViewItem_ForgottenHall.IsVisible = true;
            NavigationViewItem_PureFiction.IsVisible = true;
            NavigationViewItem_ApocalypticShadow.IsVisible = true;
            NavigationViewItem_ChallengePeak.IsVisible = true;
            // 铁道战绩图片
            Image_BattleChronicle.Source = GameRecordAssets.FromMsAppx("Assets/Image/ade9545750299456a3fcbc8c3b63521d_2941971308029698042.png");
        }
        else if (CurrentGameBiz.Game is GameBiz.nap)
        {
            NavigationViewItem_BattleChronicle.IsVisible = true;
            NavigationViewItem_InterKnotMonthlyReport.IsVisible = true;
            NavigationViewItem_ShiyuDefense.IsVisible = true;
            NavigationViewItem_DeadlyAssault.IsVisible = true;
            // 绝区零战绩图片
            Image_BattleChronicle.Source = GameRecordAssets.FromMsAppx("Assets/Image/bc8f0b7384b306c80f2a1fcca9f3d14b_8590605504999484795.png");
        }
    }


    #endregion


    #region Game Role Info


    public GameRecordRole? CurrentRole
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(AvatarUrl));
            }
        }
    }


    public List<GameRecordRole> GameRoleList { get; set => SetProperty(ref field, value); }


    public string AvatarUrl => !string.IsNullOrWhiteSpace(CurrentRole?.HeadIcon) ? CurrentRole.HeadIcon : $"ms-appx:///Assets/Image/icon_{(CurrentGameBiz.IsGlobalServer() ? "hoyolab" : "hyperion")}.png";


    private void LoadGameRoles(GameRecordRole? role = null)
    {
        try
        {
            if (role != null)
            {
                _gameRecordService.SetLastSelectGameRecordRole(CurrentGameBiz, role);
            }
            role ??= _gameRecordService.GetLastSelectGameRecordRoleOrTheFirstOne(CurrentGameBiz);
            var list = _gameRecordService.GetGameRoles(CurrentGameBiz);
            CurrentRole = role ?? list.FirstOrDefault();
            GameRoleList = list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load game roles ({gameBiz}).", CurrentGameBiz);
        }
    }


    [RelayCommand]
    private void WebLogin()
    {
        NavigateTo(typeof(LoginPage), CurrentGameBiz);
    }


    [RelayCommand]
    private async Task RefreshGameRoleInfoAsync()
    {
        try
        {
            if (CurrentRole is null)
            {
                await _gameRecordService.RefreshAllGameRolesInfoAsync();
            }
            else
            {
                await _gameRecordService.RefreshGameRoleInfoAsync(CurrentRole);
            }
            LoadGameRoles();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Refresh game role info ({gameBiz}, {uid}).", CurrentRole?.GameBiz, CurrentRole?.Uid);
            InAppToast.MainWindow?.Warning(Lang.Common_AccountError, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Refresh game role info ({gameBiz}, {uid}).", CurrentRole?.GameBiz, CurrentRole?.Uid);
            InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh game role info ({gameBiz}, {uid}).", CurrentRole?.GameBiz, CurrentRole?.Uid);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_GameRoles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is GameRecordRole role)
        {
            CurrentRole = role;
            _gameRecordService.SetLastSelectGameRecordRole(CurrentGameBiz, role);
            if (_currentPageType?.Name is not nameof(LoginPage))
            {
                NavigateTo(_currentPageType, force_navigate: true);
            }
        }
    }


    private void MenuFlyoutItem_CopyCookie_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { Tag: GameRecordRole role })
            {

                ClipboardHelper.SetText(role.Cookie);
            }
        }
        catch { }
    }


    private void MenuFlyoutItem_DeleteGameRole_Click(object sender, RoutedEventArgs e)
    {
        GameRecordRole? gameRole = null;
        try
        {
            if (sender is FrameworkElement { Tag: GameRecordRole role })
            {
                gameRole = role;
                _gameRecordService.DeleteGameRole(role);
                LoadGameRoles();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete game role ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    [RelayCommand]
    private async Task InputCookieAsync()
    {
        try
        {
            var textbox = new TextBox
            {
                IsSpellCheckEnabled = false,
            };
            var dialog = new ContentDialog
            {
                Title = Lang.HoyolabToolboxPage_InputCookie,
                Content = textbox,
                PrimaryButtonText = Lang.Common_Confirm,
                SecondaryButtonText = Lang.Common_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result is ContentDialogResult.Primary)
            {
                var cookie = textbox.Text;
                if (string.IsNullOrWhiteSpace(cookie))
                {
                    _logger.LogInformation("Input cookie is null or white space.");
                    return;
                }
                var user = await _gameRecordService.AddRecordUserAsync(cookie);
                var roles = await _gameRecordService.AddGameRolesAsync(cookie);
                InAppToast.MainWindow?.Success(null, string.Format(Lang.LoginPage_AlreadyAddedGameRoles, roles.Count, string.Join("\r\n", roles.Select(x => $"{x.Nickname}  {x.Uid}"))), 5000);
                LoadGameRoles(roles.FirstOrDefault(x => x.GameBiz == CurrentGameBiz.ToString()));
            }
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Input cookie");
            InAppToast.MainWindow?.Warning(Lang.Common_AccountError, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Input cookie");
            InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Input cookie");
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private async Task RefreshGameRoleHeadIconSilentlyAsync()
    {
        try
        {
            if (CurrentRole is not null)
            {
                await _gameRecordService.UpdateGameRoleHeadIconAsync(CurrentRole);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update game role head icon silently ({gameBiz}, {uid}).", CurrentRole?.GameBiz, CurrentRole?.Uid);
        }
    }


    #endregion


    #region Navigate


    private void NavigateTo(Type? page, object? parameter = null, bool force_navigate = false)
    {
        if (page is null)
        {
            return;
        }
        if (!force_navigate && _currentPageType == page)
        {
            return;
        }
        NavigateToPage(page, parameter ?? CurrentRole);
    }


    private Control? _currentPage;

    private Type? _currentPageType;


    /// <summary>
    /// 上游 <c>Frame.Navigate(pageType, parameter)</c> 的 Avalonia 等价物。
    /// </summary>
    /// <remarks>
    /// Linux: Avalonia 没有 Frame/Page 导航栈，这里手工实例化页面、塞进 <c>Frame_Content</c>，
    /// 并调用 <see cref="PageBase.OnNavigatedTo(object?)"/> 传参（与上游 Frame 的行为一致）。
    /// </remarks>
    private void NavigateToPage(Type pageType, object? parameter)
    {
        if (Activator.CreateInstance(pageType) is not Control page)
        {
            return;
        }
        _currentPage = page;
        _currentPageType = pageType;
        Frame_Content.Content = page;
        if (page is PageBase pageBase)
        {
            pageBase.OnNavigatedTo(parameter);
        }
    }


    // Linux: 上游 NavigationView.ItemInvoked；Avalonia 用 SplitView + ListBox，选中项即导航项。
    private void ListBox_Toolbox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is ListBox listBox && listBox.SelectedItem is ListBoxItem item)
            {
                var type = (item.Tag as string) switch
                {
                    nameof(TravelersDiaryPage) => typeof(TravelersDiaryPage),
                    nameof(SpiralAbyssPage) => typeof(SpiralAbyssPage),
                    nameof(ImaginariumTheaterPage) => typeof(ImaginariumTheaterPage),
                    nameof(StygianOnslaughtPage) => typeof(StygianOnslaughtPage),
                    nameof(TrailblazeCalendarPage) => typeof(TrailblazeCalendarPage),
                    nameof(SimulatedUniversePage) => typeof(SimulatedUniversePage),
                    nameof(ForgottenHallPage) => typeof(ForgottenHallPage),
                    nameof(PureFictionPage) => typeof(PureFictionPage),
                    nameof(ApocalypticShadowPage) => typeof(ApocalypticShadowPage),
                    nameof(ChallengePeakPage) => typeof(ChallengePeakPage),
                    nameof(InterKnotMonthlyReportPage) => typeof(InterKnotMonthlyReportPage),
                    nameof(ShiyuDefensePage) => typeof(ShiyuDefensePage),
                    nameof(DeadlyAssaultPage) => typeof(DeadlyAssaultPage),
                    _ => null,
                };
                NavigateTo(type);
            }
        }
        catch { }
    }


    private void NavigationViewItem_BattleChronicle_Click(object? sender, RoutedEventArgs e)
    {
        ShowBattleChronicleWindow();
    }


    private BattleChronicleWindow? _battleChronicleWindow;


    private void ShowBattleChronicleWindow()
    {
        // Linux: 上游用 AppWindow is null 判断窗口已关闭；Avalonia 在 Closed 里把字段置空。
        if (_battleChronicleWindow is null)
        {
            _battleChronicleWindow = new BattleChronicleWindow
            {
                CurrentRole = CurrentRole,
            };
            _battleChronicleWindow.Closed += (_, _) => _battleChronicleWindow = null;
            _battleChronicleWindow.Show();
            return;
        }
        if (_battleChronicleWindow.CurrentRole != CurrentRole)
        {
            _battleChronicleWindow.CurrentRole = CurrentRole;
        }
        _battleChronicleWindow.Activate();
    }


    #endregion


    #region Device Info


    private async void NavigationViewItem_UpdateDeviceInfo_Click(object? sender, RoutedEventArgs e)
    {
        await UpdateDeviceInfoAsync(true);
    }


    private async Task UpdateDeviceInfoAsync(bool forceUpdate = false)
    {
        try
        {
            await _gameRecordService.UpdateDeviceFpAsync(forceUpdate);
            if (forceUpdate)
            {
                InAppToast.MainWindow?.Success(Lang.HoyolabToolboxPage_TheDeviceFingerprintIsAlreadyUpdated);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update device info");
            if (forceUpdate)
            {
                InAppToast.MainWindow?.Error(ex);
            }
        }
    }


    #endregion


    public static void HandleMiHoYoApiException(miHoYoApiException ex)
    {
        if (ex.ReturnCode is 1034 or 5003 or 10035 or 10041 or 10053)
        {
            InAppToast.MainWindow?.ShowWithButton(InfoBarSeverity.Warning, Lang.Common_AccountError, ex.Message, Lang.HoyolabToolboxPage_VerifyAccount, () =>
            {
                WeakReferenceMessenger.Default.Send(new GameRecordVerifyAccountMessage());
            });
        }
        else
        {
            InAppToast.MainWindow?.Warning(Lang.Common_AccountError, ex.Message);
        }
    }


}
