using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Services;
using LunarWard.Core.GameRecord;
using LunarWard.Launch;

namespace LunarWard.ViewModels;

/// <summary>
/// 便笺窗口：粘贴 cookie → 拉取实时便笺 → 归一化展示。
/// </summary>
public sealed partial class DailyNoteViewModel : ObservableObject
{
    private readonly AppServices _services;

    private readonly DailyNoteService _service;

    private readonly ProfileItemViewModel _profile;

    private readonly string _server;

    private readonly string _gameKind;

    private readonly string _gameBiz;


    public DailyNoteViewModel(AppServices services, ProfileItemViewModel profile, DailyNoteService? service = null)
    {
        _services = services;
        _profile = profile;
        _service = service ?? new DailyNoteService(services.Paths);
        _gameKind = profile.Profile.Game;

        var definition = Games.GameCatalog.Get(
            profile.Profile.Game,
            string.Equals(profile.Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
                ? Games.GameEdition.Global
                : Games.GameEdition.China);
        _gameBiz = definition?.GameBiz ?? "hk4e_cn";
        _server = _gameBiz.EndsWith("_global", StringComparison.Ordinal) ? "global" : "cn";

        Items = new ObservableCollection<DailyNoteItemViewModel>();

        var saved = _service.GetCredentials(_server);
        Cookie = saved?.Cookie ?? "";
        Uid = saved?.Uid ?? "";
        Region = saved?.Region ?? (_server == "global" ? "os_asia" : "cn_gf01");

        StatusText = string.IsNullOrEmpty(Cookie)
            ? "还没有保存 cookie。请在米游社/HoYoLAB 网页版复制 cookie 后粘贴到上方。"
            : "已保存 cookie，点『刷新』拉取便笺。";
    }


    public string Title => _gameKind switch
    {
        "zzz" => "绝区零 · 实时便笺",
        "hkrpg" => "星穹铁道 · 实时便笺",
        _ => "原神 · 实时便笺",
    };


    public ObservableCollection<DailyNoteItemViewModel> Items { get; }


    [ObservableProperty]
    private string cookie = "";


    [ObservableProperty]
    private string uid = "";


    [ObservableProperty]
    private string region = "";


    [ObservableProperty]
    private string nickname = "";


    [ObservableProperty]
    private string primaryName = "";


    [ObservableProperty]
    private int primaryCurrent;


    [ObservableProperty]
    private int primaryMax;


    [ObservableProperty]
    private double primaryPercent;


    [ObservableProperty]
    private string recoveryText = "";


    [ObservableProperty]
    private bool? dailyTasksDone;


    [ObservableProperty]
    private string statusText = "";


    [ObservableProperty]
    private string? errorText;


    [ObservableProperty]
    private bool isBusy;


    [RelayCommand]
    private void SaveCookie()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Cookie))
            {
                ErrorText = "cookie 不能为空";
                return;
            }

            _service.SaveCredentials(_server, Cookie, Uid, Region);
            StatusText = "cookie 已保存（文件权限 0600）";
            ErrorText = null;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    [RelayCommand]
    private void ClearCookie()
    {
        _service.ClearCredentials(_server);
        Cookie = "";
        Items.Clear();
        StatusText = "cookie 已删除";
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(Cookie))
        {
            ErrorText = "先粘贴并保存 cookie";
            return;
        }

        IsBusy = true;
        ErrorText = null;
        try
        {
            var role = new GameRecordRole
            {
                Nickname = Nickname,
                Uid = long.TryParse(Uid, out var parsed) ? parsed : 0,
                Region = Region,
                GameBiz = _gameBiz,
                Cookie = Cookie,
            };

            var summary = await _service.FetchAsync(_gameKind, new LunarWard.Core.GameBiz(_gameBiz), role);

            Nickname = summary.Nickname;
            PrimaryName = summary.PrimaryName;
            PrimaryCurrent = summary.PrimaryCurrent;
            PrimaryMax = summary.PrimaryMax;
            PrimaryPercent = summary.PrimaryPercent;
            RecoveryText = summary.PrimaryRecovery is { } recovery && recovery > TimeSpan.Zero
                ? $"回满还需 {(int)recovery.TotalHours} 小时 {recovery.Minutes} 分"
                : "已回满";
            DailyTasksDone = summary.DailyTasksDone;

            Items.Clear();
            foreach (var item in summary.Items)
            {
                Items.Add(new DailyNoteItemViewModel(item));
            }

            StatusText = $"更新于 {summary.FetchedAt:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}


public sealed class DailyNoteItemViewModel
{
    public DailyNoteItemViewModel(DailyNoteItem item)
    {
        Item = item;
    }


    public DailyNoteItem Item { get; }

    public string Name => Item.Name;

    public string Value => Item.Value;
}
