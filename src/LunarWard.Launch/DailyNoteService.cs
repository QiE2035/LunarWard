using System.Text.Json;
using System.Text.Json.Serialization;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.Genshin.DailyNote;
using LunarWard.Core.GameRecord.ZZZ.DailyNote;
using LunarWard.Platform;

namespace LunarWard.Launch;

/// <summary>
/// 便笺（实时便笺 / 开拓力 / 电量）的统一形态。
/// </summary>
/// <remarks>
/// 原神叫"树脂"、绝区零叫"电量"，字段语义相似但结构完全不同。
/// 这里归一成 <see cref="Items"/> 列表，UI 只认 <c>(Name, Current, Max)</c>。
/// </remarks>
public sealed record DailyNoteSummary
{
    public required string GameKind { get; init; }

    public required string Nickname { get; init; }

    public required long Uid { get; init; }

    public string? Region { get; init; }

    /// <summary>主要资源（树脂 / 电量）。</summary>
    public required string PrimaryName { get; init; }

    public required int PrimaryCurrent { get; init; }

    public required int PrimaryMax { get; init; }

    /// <summary>回满所需时间；<c>null</c> 表示未知。</summary>
    public TimeSpan? PrimaryRecovery { get; init; }

    /// <summary>其他条目（委托、探索派遣、每日任务…）。</summary>
    public IReadOnlyList<DailyNoteItem> Items { get; init; } = [];

    /// <summary>全部每日任务是否完成。</summary>
    public bool? DailyTasksDone { get; init; }

    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.Now;


    public double PrimaryPercent => PrimaryMax <= 0 ? 0 : (double)PrimaryCurrent / PrimaryMax;
}


public sealed record DailyNoteItem(string Name, string Value, bool? Done = null);


/// <summary>
/// 便笺服务：把 Core 的两种数据结构归一成 <see cref="DailyNoteSummary"/>。
/// </summary>
/// <remarks>
/// <para>
/// <b>Cookie 从哪来</b>：上游 Starward 读游戏的注册表项（<c>MIHOYOSDK_ADL_*</c>）。
/// Linux 下同样的数据在 Wine prefix 的 <c>user.reg</c> 里，
/// 但那是二进制 blob，且不同游戏版本键名会漂移。首期采用
/// <b>用户手动粘贴 cookie</b> + 本地受限文件保存，避免"读不到就静默失败"。
/// </para>
/// <para>
/// <b>安全</b>：cookie 属于账号凭据。保存文件权限 <c>0600</c>，且只落在
/// <c>$XDG_DATA_HOME/lunarward/</c>；不写日志。
/// </para>
/// </remarks>
public sealed class DailyNoteService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };


    private readonly HttpClient _httpClient;

    private readonly string _storePath;


    public DailyNoteService(IXdgPaths paths, HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _storePath = Path.Join(paths.DataHome, "daily-note-credentials.json");
    }


    /// <summary>凭据文件路径。</summary>
    public string StorePath => _storePath;


    /// <summary>
    /// 保存凭据（<paramref name="cookie"/> 必须非空）。
    /// </summary>
    public void SaveCredentials(string server, string cookie, string? uid = null, string? serverRegion = null)
    {
        if (string.IsNullOrWhiteSpace(cookie))
        {
            throw new ArgumentException("cookie 不能为空", nameof(cookie));
        }

        var store = LoadStore();
        store[$"{server}"] = new CredentialEntry
        {
            Cookie = cookie.Trim(),
            Uid = uid,
            Region = serverRegion,
        };
        SaveStore(store);
    }


    public CredentialEntry? GetCredentials(string server) => LoadStore().GetValueOrDefault(server);


    public void ClearCredentials(string server)
    {
        var store = LoadStore();
        if (store.Remove(server))
        {
            SaveStore(store);
        }
    }


    /// <summary>
    /// 拉取便笺。<paramref name="server"/> 决定用哪个客户端：
    /// <c>cn</c> → <see cref="HyperionClient"/>；<c>global</c> → <see cref="HoyolabClient"/>。
    /// </summary>
    public async Task<DailyNoteSummary> FetchAsync(
        string gameKind,
        GameBiz gameBiz,
        GameRecordRole role,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(role.Cookie))
        {
            throw new InvalidOperationException("缺少 cookie：请先在便笺面板里粘贴");
        }

        // 国内用 Hyperion（米游社），国际用 Hoyolab。
        // 语言通过 X-Rpc-Language 头传给 API，由具体请求方法决定；
        // 这两个客户端的构造不带语言参数（上游也是这样）。
        GameRecordClient client = gameBiz.Server == "global"
            ? new HoyolabClient(_httpClient) { Language = "zh-cn" }
            : new HyperionClient(_httpClient);

        return gameKind switch
        {
            "zzz" => ToSummary(await client.GetZZZDailyNoteAsync(role, ct).ConfigureAwait(false), role),
            "hkrpg" => ToSummary(await client.GetStarRailDailyNoteAsync(role, ct).ConfigureAwait(false), role),
            _ => ToSummary(await client.GetGenshinDailyNoteAsync(role, ct).ConfigureAwait(false), role),
        };
    }


    /// <summary>原神 → 统一形态。</summary>
    public static DailyNoteSummary ToSummary(GenshinDailyNote note, GameRecordRole role) => new()
    {
        GameKind = "genshin",
        Nickname = role.Nickname ?? "",
        Uid = role.Uid,
        Region = role.Region,
        PrimaryName = "原粹树脂",
        PrimaryCurrent = note.CurrentResin,
        PrimaryMax = note.MaxResin,
        PrimaryRecovery = note.ResinRecoveryTime,
        DailyTasksDone = note.FinishedTaskNum >= note.TotalTaskNum && note.TotalTaskNum > 0,
        Items =
        [
            new DailyNoteItem("每日委托", $"{note.FinishedTaskNum}/{note.TotalTaskNum}"),
            new DailyNoteItem("探索派遣", $"{note.CurrentExpeditionNum}/{note.MaxExpeditionNum}"),
            new DailyNoteItem("洞天宝钱", $"{note.CurrentHomeCoin}/{note.MaxHomeCoin}"),
            new DailyNoteItem("周本减半", $"{note.RemainResinDiscountNum}/{note.ResinDiscountNumLimit}"),
        ],
    };


    /// <summary>绝区零 → 统一形态。</summary>
    public static DailyNoteSummary ToSummary(ZZZDailyNote note, GameRecordRole role) => new()
    {
        GameKind = "zzz",
        Nickname = role.Nickname ?? "",
        Uid = role.Uid,
        Region = role.Region,
        PrimaryName = "电量",
        PrimaryCurrent = note.Energy?.Progress?.Current ?? 0,
        PrimaryMax = note.Energy?.Progress?.Max ?? 0,
        PrimaryRecovery = note.Energy?.Restore ?? default,
        // 上游 `IsDailyMissionComplete` 直接解引用 Vitality/VhsSale；
        // 接口偶尔省字段时会 NRE，这里自算而不是直接调它。
        DailyTasksDone = note.Vitality is { } vitality
                         && note.VhsSale is { IsSaleStateDoing: true }
                         && note.IsCardSignDone
                         && vitality.Current >= vitality.Max,
        Items =
        [
            new DailyNoteItem("活跃度", $"{note.Vitality?.Current ?? 0}/{note.Vitality?.Max ?? 0}"),
            new DailyNoteItem("录像店营业", note.VhsSale?.IsSaleStateDoing == true ? "营业中" : "未营业"),
            new DailyNoteItem("刮刮卡", note.IsCardSignDone ? "已完成" : "未完成"),
        ],
    };


    /// <summary>星穹铁道 → 统一形态。</summary>
    public static DailyNoteSummary ToSummary(LunarWard.Core.GameRecord.StarRail.DailyNote.StarRailDailyNote note, GameRecordRole role) => new()
    {
        GameKind = "hkrpg",
        Nickname = role.Nickname ?? "",
        Uid = role.Uid,
        Region = role.Region,
        PrimaryName = "开拓力",
        PrimaryCurrent = note.CurrentStamina,
        PrimaryMax = note.MaxStamina,
        PrimaryRecovery = note.StaminaRecoverTime,
        DailyTasksDone = note.MaxTrainScore > 0 && note.CurrentTrainScore >= note.MaxTrainScore,
        Items =
        [
            new DailyNoteItem("每日实训", $"{note.CurrentTrainScore}/{note.MaxTrainScore}"),
            new DailyNoteItem("支援委托", $"{note.AcceptedExpeditionNum}/{note.TotalExpeditionNum}"),
            new DailyNoteItem("历战余响", $"{note.WeeklyCocoonCnt}/{note.WeeklyCocoonLimit}"),
            new DailyNoteItem("后备开拓力", note.CurrentReserveStamina.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ],
    };


    private Dictionary<string, CredentialEntry> LoadStore()
    {
        try
        {
            if (!File.Exists(_storePath))
            {
                return new Dictionary<string, CredentialEntry>(StringComparer.Ordinal);
            }

            return JsonSerializer.Deserialize<Dictionary<string, CredentialEntry>>(
                       File.ReadAllText(_storePath), Options)
                   ?? new Dictionary<string, CredentialEntry>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, CredentialEntry>(StringComparer.Ordinal);
        }
    }


    private void SaveStore(Dictionary<string, CredentialEntry> store)
    {
        AtomicFile.WriteAllText(_storePath, JsonSerializer.Serialize(store, Options));
        try
        {
            // cookie 是账号凭据：只允许本人读写
            File.SetUnixFileMode(_storePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
            // 某些文件系统不支持，忽略
        }
    }


    /// <summary>一条凭据。</summary>
    public sealed class CredentialEntry
    {
        public string Cookie { get; set; } = "";

        public string? Uid { get; set; }

        public string? Region { get; set; }
    }
}
