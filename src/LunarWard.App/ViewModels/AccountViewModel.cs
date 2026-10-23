using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.Core;
using LunarWard.Games;
using LunarWard.Launch;
using LunarWard.Platform;

namespace LunarWard.App.ViewModels;

/// <summary>
/// 账号切换窗口。
/// </summary>
/// <remarks>
/// <para>
/// 机制（与上游一致）：游戏把登录态写进注册表的
/// <c>MIHOYOSDK_ADL_PROD_*</c> 二进制值；"切换账号" = 把这个 blob 写回去。
/// Linux 下同样的数据在 Wine prefix 的 <c>user.reg</c> 的 <c>hex:</c> 值里。
/// </para>
/// <para>
/// <b>安全</b>：这些 blob 内含 cookie 明文。默认只保存在本机
/// <c>$XDG_DATA_HOME/lunarward/accounts.json</c>，权限 0600，不写日志、不上传。
/// </para>
/// </remarks>
public sealed partial class AccountViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions StoreOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };


    private readonly AppServices _services;

    private readonly ProfileItemViewModel _profile;

    private readonly WineAccountService? _wine;

    private readonly string _gameBiz;

    private readonly string _storePath;


    public AccountViewModel(AppServices services, ProfileItemViewModel profile)
    {
        _services = services;
        _profile = profile;
        _gameBiz = Games.GameCatalog.Get(
            profile.Profile.Game,
            string.Equals(profile.Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
                ? Games.GameEdition.Global
                : Games.GameEdition.China)?.GameBiz ?? "hk4e_cn";

        var prefix = PrefixResolver.Resolve(
            profile.Profile.Prefix.ToDomain(), profile.Id, services.Paths);
        _wine = new WineAccountService(prefix);
        _storePath = Path.Join(services.Paths.DataHome, "accounts.json");

        Accounts = new ObservableCollection<SavedAccountViewModel>();
        Reload();
    }


    public ObservableCollection<SavedAccountViewModel> Accounts { get; }

    public string Title => $"{_gameBiz} · 账号切换";

    public string Prefix => _wine?.UserRegPath ?? "(prefix 不存在)";


    [ObservableProperty]
    private SavedAccountViewModel? selected;


    [ObservableProperty]
    private string statusText = "";


    [ObservableProperty]
    private string? errorText;


    /// <summary>当前 prefix 里的账号快照（显示用）。</summary>
    [ObservableProperty]
    private string currentAccountText = "";


    [RelayCommand]
    private void Reload()
    {
        Accounts.Clear();
        foreach (var account in LoadStore())
        {
            if (account.GameBiz == _gameBiz)
            {
                Accounts.Add(new SavedAccountViewModel(account));
            }
        }

        RefreshCurrent();
        StatusText = Accounts.Count == 0
            ? "还没有保存的账号。点『保存当前账号』把游戏当前的登录态存下来。"
            : $"已保存 {Accounts.Count} 个账号";
    }


    private void RefreshCurrent()
    {
        var current = _wine?.Read(_gameBiz);
        if (current is null)
        {
            CurrentAccountText = "读不到当前账号（prefix 里没有该游戏的注册表项）";
            return;
        }

        CurrentAccountText = $"当前：{current.Sha256[..12]} · {current.Value.Length} 字节"
                             + (current.Uid > 0 ? $" · UID {current.Uid}" : "");
    }


    [RelayCommand]
    private void SaveCurrent()
    {
        if (_wine is null)
        {
            ErrorText = "prefix 不存在";
            return;
        }

        if (!_wine.IsAvailable)
        {
            ErrorText = $"prefix 里没有 user.reg：{_wine.UserRegPath}";
            return;
        }

        var snapshot = _wine.Read(_gameBiz);
        if (snapshot is null)
        {
            ErrorText = "读不到当前账号。请先在游戏里登录一次，再回来保存。";
            return;
        }

        snapshot.Name = snapshot.Uid > 0 ? $"UID {snapshot.Uid}" : snapshot.Sha256[..8];

        var all = LoadStore();
        if (all.Any(x => x.Sha256 == snapshot.Sha256 && x.GameBiz == snapshot.GameBiz))
        {
            ErrorText = "这个账号已经保存过了";
            return;
        }

        all.Add(snapshot);
        SaveStore(all);
        Reload();
        StatusText = $"已保存账号 {snapshot.Name}";
        ErrorText = null;
    }


    [RelayCommand]
    private void Switch()
    {
        if (Selected is null)
        {
            ErrorText = "先选一个账号";
            return;
        }

        if (_wine is null)
        {
            ErrorText = "prefix 不存在";
            return;
        }

        var error = _wine.Write(Selected.Snapshot);
        if (error is not null)
        {
            ErrorText = error;
            return;
        }

        StatusText = $"已切换到 {Selected.Name}";
        ErrorText = null;
        RefreshCurrent();
    }


    [RelayCommand]
    private void Delete()
    {
        if (Selected is null)
        {
            ErrorText = "先选一个账号";
            return;
        }

        var all = LoadStore();
        all.RemoveAll(x => x.Sha256 == Selected.Snapshot.Sha256 && x.GameBiz == Selected.Snapshot.GameBiz);
        SaveStore(all);
        Reload();
        StatusText = "已删除";
    }


    [RelayCommand]
    private void Rename()
    {
        if (Selected is null)
        {
            return;
        }

        var all = LoadStore();
        var index = all.FindIndex(x => x.Sha256 == Selected.Snapshot.Sha256 && x.GameBiz == Selected.Snapshot.GameBiz);
        if (index < 0)
        {
            return;
        }

        all[index].Name = Selected.Name;
        SaveStore(all);
        StatusText = "已重命名";
    }


    private List<GameAccountSnapshot> LoadStore()
    {
        try
        {
            if (!File.Exists(_storePath))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<GameAccountSnapshot>>(
                       File.ReadAllText(_storePath), StoreOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }


    private void SaveStore(List<GameAccountSnapshot> accounts)
    {
        try
        {
            AtomicFile.WriteAllText(_storePath, JsonSerializer.Serialize(accounts, StoreOptions));
            File.SetUnixFileMode(_storePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex)
        {
            ErrorText = $"保存失败：{ex.Message}";
        }
    }
}


/// <summary>一个已保存账号的展示项。</summary>
public sealed partial class SavedAccountViewModel : ObservableObject
{
    public SavedAccountViewModel(GameAccountSnapshot snapshot)
    {
        Snapshot = snapshot;
    }


    public GameAccountSnapshot Snapshot { get; }

    public string Name
    {
        get => Snapshot.Name;
        set
        {
            Snapshot.Name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Display));
        }
    }


    public string Display => $"{Name} · {Snapshot.Sha256[..12]}";

    public string Detail => Snapshot.Uid > 0
        ? $"UID {Snapshot.Uid} · {Snapshot.Value.Length} 字节"
        : $"{Snapshot.Value.Length} 字节";

    public string SavedAt => Snapshot.Time.ToString("yyyy-MM-dd HH:mm");
}
