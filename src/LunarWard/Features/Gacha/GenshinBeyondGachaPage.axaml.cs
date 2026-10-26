using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using LunarWard.Core;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Features.GameLauncher;
using LunarWard.Frameworks;
using LunarWard.Helpers;
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
using System.Windows.Input;

namespace LunarWard.Features.Gacha;

/// <summary>
/// 千星奇域（原神 UGC）抽卡记录页，对齐上游 <c>Starward/Features/Gacha/GenshinBeyondGachaPage.xaml(.xaml.cs)</c>。
/// </summary>
/// <remarks>
/// 平台替换点（见 docs/09 的「七、抽卡与云游戏」）：
/// <c>InfoBar</c> 进度条 + 取消按钮 → <see cref="GachaProgressToast"/>；
/// <c>ContentDialog</c> → <see cref="Services.DialogService"/> + <see cref="Frameworks.Dialogs.StarwardContentDialog"/>；
/// <c>Launcher.LaunchFolderAsync(FolderLauncherOptions)</c> → <see cref="RevealInFileManager"/>（xdg-open）；
/// <c>StorageFile/StorageFolder</c> 校验 → <see cref="File"/>/<see cref="Directory"/>；
/// <c>FontIcon</c> → <c>TextBlock Classes="icon"</c>；<c>Visibility</c> → <see cref="Avalonia.Visual.IsVisibleProperty"/>；
/// <c>[ObservableProperty]</c>/<c>[RelayCommand]</c> → 手写属性与 <see cref="IRelayCommand"/>。
/// </remarks>
public sealed partial class GenshinBeyondGachaPage : PageBase, INotifyPropertyChanged
{

    private readonly ILogger<GenshinBeyondGachaPage> _logger = AppConfig.GetLogger<GenshinBeyondGachaPage>();

    private readonly GenshinBeyondGachaService _gachaLogService = AppConfig.GetService<GenshinBeyondGachaService>();



    public GenshinBeyondGachaPage()
    {
        this.InitializeComponent();
        DataContext = this;

        UpdateGachaLogCommand = new AsyncRelayCommand<string?>(UpdateGachaLogAsync);
        InputUrlCommand = new AsyncRelayCommand(InputUrlAsync);
        CopyUrlCommand = new AsyncRelayCommand(CopyUrlAsync);
        DeleteGachaCacheFolderCommand = new AsyncRelayCommand(DeleteGachaCacheFolderAsync);
        OpenItemStatsPaneCommand = new RelayCommand(OpenItemStatsPane);
    }


    public ICommand UpdateGachaLogCommand { get; }

    public ICommand InputUrlCommand { get; }

    public ICommand CopyUrlCommand { get; }

    public ICommand DeleteGachaCacheFolderCommand { get; }

    public ICommand OpenItemStatsPaneCommand { get; }


    private ObservableCollection<long> _uidList = new();

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
                OnSelectUidChanged(value);
            }
        }
    }


    private void OnSelectUidChanged(long? value)
    {
        AppConfig.SetLastUidInGachaLogPage("hk4eugc", value ?? 0);
        UpdateGachaTypeStats(value);
    }




    protected override async void OnLoaded()
    {
        await Task.Delay(16);
        Initialize();
        await UpdateGachaInfoAsync();
    }



    protected override void OnUnloaded()
    {
        GachaStatsType1000 = null;
        GachaStatsType2000 = null;
        GachaItemStats = null;
    }



    private void Initialize()
    {
        try
        {
            SelectUid = null;
            UidList = new(_gachaLogService.GetUids());
            var lastUid = AppConfig.GetLastUidInGachaLogPage("hk4eugc");
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


    public GenshinBeyondGachaTypeStats? GachaStatsType1000 { get; set => SetProperty(ref field, value); }

    public GenshinBeyondGachaTypeStats? GachaStatsType2000 { get; set => SetProperty(ref field, value); }

    public List<GenshinBeyondGachaItemEx>? GachaItemStats { get; set => SetProperty(ref field, value); }


    private int errorCount = 0;


    private void UpdateGachaTypeStats(long? uid)
    {
        try
        {
            GachaStatsType1000 = null;
            GachaStatsType2000 = null;

            if (uid.HasValue && uid.Value != 0)
            {
                GachaStatsType1000 = _gachaLogService.GetGachaTypeStatsType1000(uid.Value);
                GachaStatsType2000 = _gachaLogService.GetGachaTypeStatsType2000(uid.Value);
                GachaItemStats = _gachaLogService.GetGachaItemStats(uid.Value);
            }

            if (GachaStatsType1000 is null && GachaStatsType2000 is null)
            {
                StackPanel_Emoji.IsVisible = true;
            }
            else
            {
                StackPanel_Emoji.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateGachaTypeStats");
        }
    }




    private async Task UpdateGachaInfoAsync()
    {
        try
        {
            await _gachaLogService.UpdateGachaInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update wiki data hk4eugc");
        }
    }



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
            GachaProgressToast? toast = GachaProgressToast.Show(cancelSource);
            var progress = new Progress<string>((str) =>
            {
                if (toast is not null)
                {
                    toast.Message = str;
                }
            });
            var newUid = await _gachaLogService.GetGachaLogAsync(url, all, GachaLanguage, progress, cancelSource.Token);
            GachaProgressToast.Close(toast, $"Uid {newUid}", InfoBarSeverity.Success);
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



    private async Task InputUrlAsync()
    {
        try
        {
            var textbox = new TextBox();
            var dialog = new Frameworks.Dialogs.StarwardContentDialog
            {
                // 输入 URL
                Title = Lang.GachaLogPage_InputURL,
                Content = textbox,
                // 确认
                PrimaryButtonText = Lang.Common_Confirm,
                // 取消
                SecondaryButtonText = Lang.Common_Cancel,
            };
            bool primary = false;
            dialog.PrimaryButtonClicked += (_, _) => primary = true;
            await Services.DialogService.ShowAsync(dialog, this);
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



    public string? GachaLanguage
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.GachaLanguage = value;
            }
        }
    } = AppConfig.GachaLanguage;



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



    private async Task DeleteGachaCacheFolderAsync()
    {
        try
        {
            var installPath = GameLauncherService.GetGameInstallPath(CurrentGameId);
            if (Directory.Exists(installPath))
            {
                var webCachesPath = GenshinBeyondGachaClient.GetWebCachesFolderPath(CurrentGameBiz, installPath);
                if (Directory.Exists(webCachesPath))
                {
                    RevealInFileManager(webCachesPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete gacha cache file");
        }
    }



    /// <summary>
    /// 上游 <c>Launcher.LaunchFolderAsync(await folder.GetParentAsync(), FolderLauncherOptions{ItemsToSelect})</c>：
    /// 在文件管理器中打开父目录并选中目标。Linux 桌面没有等价的「选中」接口，这里打开父目录。
    /// </summary>
    private static void RevealInFileManager(string path)
    {
        string folder = Path.GetDirectoryName(path) ?? path;
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



    private bool IsGachaCacheFileExists()
    {
        try
        {
            var installPath = GameLauncherService.GetGameInstallPath(CurrentGameId);
            if (Directory.Exists(installPath))
            {
                var path = GenshinBeyondGachaClient.GetGachaCacheFilePath(CurrentGameBiz, installPath);
                return File.Exists(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check gacha cache file exists");
        }
        return false;
    }



    private void OpenItemStatsPane()
    {
        SplitView_Content.IsPaneOpen = true;
    }




    #region Segmented

    // 上游用 CommunityToolkit 的 cwc:Segmented 双向绑定 Pivot.SelectedIndex；
    // Avalonia 没有 Segmented，这里用两个 ToggleButton + TabControl（隐藏表头）等价实现。

    private void SegmentedItem_1000_Click(object? sender, RoutedEventArgs e)
    {
        Segmented_GachaItemList1000.SelectedIndex = sender == ToggleButton_1000_Rarity4 ? 0 : 1;
        SyncSegmented1000();
    }

    private void SegmentedItem_2000_Click(object? sender, RoutedEventArgs e)
    {
        Segmented_GachaItemList2000.SelectedIndex = sender == ToggleButton_2000_Rarity5 ? 0 : 1;
        SyncSegmented2000();
    }

    private void Segmented_GachaItemList1000_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SyncSegmented1000();
    }

    private void Segmented_GachaItemList2000_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SyncSegmented2000();
    }

    private void SyncSegmented1000()
    {
        ToggleButton_1000_Rarity4.IsChecked = Segmented_GachaItemList1000.SelectedIndex == 0;
        ToggleButton_1000_Rarity3.IsChecked = Segmented_GachaItemList1000.SelectedIndex == 1;
    }

    private void SyncSegmented2000()
    {
        ToggleButton_2000_Rarity5.IsChecked = Segmented_GachaItemList2000.SelectedIndex == 0;
        ToggleButton_2000_Rarity4.IsChecked = Segmented_GachaItemList2000.SelectedIndex == 1;
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
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }

    #endregion

}
