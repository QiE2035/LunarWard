using Avalonia.Interactivity;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
using System.Threading.Tasks;


namespace LunarWard.Features.Gacha.UIGF;

/// <summary>
/// 上游 <c>Starward.Features.Gacha.UIGF.UIGF4GachaWindow</c> 的移植。
/// </summary>
/// <remarks>
/// 平台差异（详见 docs/09-LINUX-DIVERGENCES.md）：
/// <c>AppWindow.TitleBar.ExtendsContentIntoTitleBar</c>、<c>DesktopAcrylicBackdrop</c>、
/// <c>Launcher.LaunchFolderAsync(FolderLauncherOptions.ItemsToSelect)</c> 无 Avalonia 等价物；
/// 导出后用 <c>xdg-open</c> 打开所在目录（与仓库既有做法一致）。
/// </remarks>
public sealed partial class UIGF4GachaWindow : WindowEx, INotifyPropertyChanged
{

    private readonly ILogger<UIGF4GachaWindow> _logger = AppConfig.GetLogger<UIGF4GachaWindow>();

    private readonly UIGFGachaService _uigfGachaService = AppConfig.GetService<UIGFGachaService>();



    public UIGF4GachaWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
    }




    private void InitializeWindow()
    {
        // 上游：AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;（Avalonia 由窗口管理器绘制）
        Title = Lang.ToolboxSetting_GachaRecordsImportExport;
        if (!ShouldAppsUseDarkMode())
        {
            RootGrid.RequestedThemeVariant = ThemeVariant.Light;
        }
        // 上游：SystemBackdrop = new DesktopAcrylicBackdrop();（Avalonia 无等价 API，docs/09）
        AdaptTitleBarButtonColorToAcutallTheme();
        SetIcon();
        CenterInScreen(1000, 600);
        DataContext = this;
    }



    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var list = _uigfGachaService.GetLocalGachaArchives();
            foreach (var item in list)
            {
                GachaExportArchives.Add(item);
            }
        }
        catch (Exception ex)
        {
            ExportError = ex.Message;
        }
    }



    private void RootGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        GachaExportArchives.Clear();
        GachaImportArchives.Clear();
        Pivot_ExportImport.Items.Clear();
    }




    #region Export


    public ObservableCollection<GachaUidArchiveDisplay> GachaExportArchives { get; } = new();


    public string? ExportError { get; set => SetProperty(ref field, value); }


    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            ExportError = null;
            if (ListView_Export.SelectedItems?.Count > 0)
            {
                string name = $"Starward_UIGF_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.json";
                string? path = await FileDialogHelper.OpenSaveFileDialogAsync(this, name, ("JSON", ".json"));
                if (!string.IsNullOrWhiteSpace(path))
                {
                    await _uigfGachaService.ExportUIGF4Async(path, ListView_Export.SelectedItems!.Cast<GachaUidArchiveDisplay>());
                    // 上游：StorageFile + FolderLauncherOptions.ItemsToSelect + Launcher.LaunchFolderAsync
                    // Avalonia 无等价 API，改为用文件管理器打开所在目录（docs/09）。
                    RevealInFileManager(path);
                }
            }
        }
        catch (Exception ex)
        {
            ExportError = ex.Message;
            _logger.LogError(ex, "Export uigf4");
        }
    }


    /// <summary>上游 <c>Launcher.LaunchFolderAsync</c>（选中文件）的 Linux 等价物：打开所在目录。</summary>
    private static void RevealInFileManager(string path)
    {
        try
        {
            string? folder = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(folder))
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


    #endregion




    #region Import



    public ObservableCollection<GachaUidArchiveDisplay> GachaImportArchives { get; } = new();



    public string? ImportError { get; set => SetProperty(ref field, value); }



    [RelayCommand]
    private async Task SelectFileAsync()
    {
        try
        {
            ImportError = null;
            string? path = await FileDialogHelper.PickSingleFileAsync(this, ("JSON", ".json"));
            if (File.Exists(path))
            {
                GachaImportArchives.Clear();
                var list = await _uigfGachaService.ImportFileAsync(path);
                foreach (var item in list)
                {
                    GachaImportArchives.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            ImportError = ex.Message;
            _logger.LogError(ex, "Select uigf4 file");
        }
    }



    [RelayCommand]
    private async Task ImportAsync()
    {
        try
        {
            ImportError = null;
            if (ListView_Import.SelectedItems?.Count > 0)
            {
                await _uigfGachaService.ImportAsync(ListView_Import.SelectedItems!.Cast<GachaUidArchiveDisplay>());
            }
        }
        catch (Exception ex)
        {
            ImportError = ex.Message;
            _logger.LogError(ex, "Import uigf4 gacha");
        }
    }



    #endregion


    #region INotifyPropertyChanged

    // 上游用 [INotifyPropertyChanged]（WinUI Window 不实现 INPC）。Avalonia 的 Window 已经实现了
    // INotifyPropertyChanged，无法再套 Toolkit 的源生成器，因此手写与 SettingPageBase 相同的
    // SetProperty/OnPropertyChanged（docs/09）。

    /// <summary>
    /// Avalonia 的 <see cref="AvaloniaObject"/> 已实现 <see cref="INotifyPropertyChanged"/>，但只对其
    /// AvaloniaProperty 发通知；本类的 CLR 属性走这里。用 <c>new</c> 隐藏基类事件，并把接口分发显式重映射过来。
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

    #endregion


}
