using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 下载页（复刻 Starward <c>DownloadSetting.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 这里是"默认安装路径 / 硬链接 / 限速"三块可写配置。LunarWard 的
/// <c>LunarWard.Installer</c> 把并发、超时、重试写成常量（与上游 <c>Starward.RPC</c>
/// 一致），没有可写项——所以这一页**如实展示默认值 + 说明**，不伪造输入框。
/// </para>
/// <para>
/// 真正可操作的只有"托管组件源"：<c>ComponentInstaller.UpdateIndexAsync</c>，
/// 与旧设置窗口的「更新索引」是同一个入口。
/// </para>
/// </remarks>
public sealed partial class SettingDownloadViewModel : SettingSectionViewModel
{
    public SettingDownloadViewModel(PageContext context, bool showDivider = false)
        : base(context, SettingSectionKind.Download, "Settings.Download", "\uE896", showDivider)
    {
        ConcurrencyValue = string.Format(
            CultureInfo.CurrentCulture,
            Strings["Settings.Download.ConcurrencyValue"]);

        RefreshIndexState();
    }


    // ── 只读默认值（Installer 里的常量）────────────────────────────────

    public string ConcurrencyValue { get; }

    public string TimeoutValue => Strings["Settings.Download.TimeoutValue"];

    public string RetriesValue => Strings["Settings.Download.RetriesValue"];

    public string ComponentSource => Wine.ComponentInstaller.IndexBaseUrl;


    // ── 本地索引状态 ──────────────────────────────────────────────────

    [ObservableProperty]
    private string _indexState = "";


    [ObservableProperty]
    private string _componentCount = "";


    public ObservableCollection<string> WineGroups { get; } = [];


    private void RefreshIndexState()
    {
        try
        {
            var index = Services.ComponentInstaller.Repository.ReadIndex();
            var indexPath = Path.Join(Services.Paths.ComponentsDirectory, "components.json");
            if (!File.Exists(indexPath))
            {
                IndexState = Strings["Settings.Download.NeverUpdated"];
                ComponentCount = "";
                WineGroups.Clear();
                return;
            }

            var stamp = File.GetLastWriteTime(indexPath);

            IndexState = string.Format(
                CultureInfo.CurrentCulture,
                Strings["Settings.Download.IndexState"],
                stamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture));

            var wineCount = index.Wine.Sum(g => Services.ComponentInstaller.Repository.ReadGroup("wine", g.Name).Count);
            var dxvkCount = index.Dxvk.Sum(g => Services.ComponentInstaller.Repository.ReadGroup("dxvk", g.Name).Count);
            ComponentCount = string.Format(
                CultureInfo.CurrentCulture,
                Strings["Settings.Download.ComponentCount"],
                wineCount,
                dxvkCount);

            WineGroups.Clear();
            foreach (var group in index.Wine)
            {
                WineGroups.Add(group.Name);
            }
        }
        catch (Exception ex)
        {
            IndexState = ex.Message;
            ComponentCount = "";
        }
    }


    [ObservableProperty]
    private bool _isUpdating;


    [ObservableProperty]
    private string? _status;


    [RelayCommand(CanExecute = nameof(CanUpdateIndex))]
    private async Task UpdateIndexAsync()
    {
        IsUpdating = true;
        Status = Strings["Settings.Download.Updating"];
        try
        {
            var progress = new Progress<string>(line => Status = $"{Strings["Settings.Download.Updating"]}  {line}");
            var result = await Services.ComponentInstaller.UpdateIndexAsync(progress).ConfigureAwait(true);
            RefreshIndexState();
            Status = result.Success
                ? string.Format(CultureInfo.CurrentCulture, Strings["Settings.Download.IndexUpdated"], result.UpdatedFiles)
                : string.Format(CultureInfo.CurrentCulture, Strings["Settings.Download.IndexFailed"],
                    string.Join("; ", result.Errors));
        }
        catch (Exception ex)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings["Settings.Download.IndexFailed"], ex.Message);
        }
        finally
        {
            IsUpdating = false;
            UpdateIndexCommand.NotifyCanExecuteChanged();
        }
    }


    private bool CanUpdateIndex() => !IsUpdating;


    partial void OnIsUpdatingChanged(bool value) => UpdateIndexCommand.NotifyCanExecuteChanged();
}
