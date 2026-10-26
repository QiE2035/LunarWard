using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Frameworks;

namespace LunarWard.Features.Setting;

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
public sealed partial class DownloadSetting : PageBase
{

    public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private static AppServices Services => AppServices.Current;

    private static Localization.AppStrings Strings => Localization.AppStrings.Instance;

    /// <summary>错误/状态行（上游各设置页末尾的 InfoBar 文本）。</summary>
    public string? Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    private string? _status;

    private static Services.LogService Log => AppServices.Current.Log;
    public DownloadSetting()
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();
        ConcurrencyValue = string.Format(
            CultureInfo.CurrentCulture,
            Strings["Settings.Download.ConcurrencyValue"]);

        RefreshIndexState();
        DataContext = this;
    }


    // ── 只读默认值（Installer 里的常量）────────────────────────────────

    public string ConcurrencyValue { get; }

    public string TimeoutValue => Strings["Settings.Download.TimeoutValue"];

    public string RetriesValue => Strings["Settings.Download.RetriesValue"];

    public string ComponentSource => Wine.ComponentInstaller.IndexBaseUrl;


    // ── 本地索引状态 ──────────────────────────────────────────────────

    private string _indexState = "";

    public string IndexState
    {
        get => _indexState;
        set => SetProperty(ref _indexState, value);
    }


    private string _componentCount = "";

    public string ComponentCount
    {
        get => _componentCount;
        set
        {
            if (SetProperty(ref _componentCount, value))
            {
                OnPropertyChanged(nameof(HasComponentCount));
            }
        }
    }

    public bool HasComponentCount => !string.IsNullOrEmpty(ComponentCount);


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


    private bool _isUpdating;

    public bool IsUpdating
    {
        get => _isUpdating;
        set
        {
            if (SetProperty(ref _isUpdating, value))
            {
                OnIsUpdatingChanged(value);
            }
        }
    }


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


    private void OnIsUpdatingChanged(bool value) => UpdateIndexCommand.NotifyCanExecuteChanged();
}
