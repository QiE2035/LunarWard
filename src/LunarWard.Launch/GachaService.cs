using LunarWard.Core;
using LunarWard.Core.Gacha;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Core.Gacha.ZZZ;

namespace LunarWard.Launch;

/// <summary>一条抽卡记录（跨游戏统一形态）。</summary>
public sealed record GachaRecord
{
    public required long Id { get; init; }

    public required string Name { get; init; }

    public required string ItemType { get; init; }

    /// <summary>3 / 4 / 5 星。</summary>
    public required int RankType { get; init; }

    public required int GachaType { get; init; }

    public required string GachaTypeName { get; init; }

    public required DateTime Time { get; init; }

    public required long Uid { get; init; }

    /// <summary>是否 UP 角色/武器（接口的 <c>is_up</c>；原神老接口没有，恒为 false）。</summary>
    public bool IsUp { get; init; }

    /// <summary>物品 id（UIGF/SRGF 导出需要）。</summary>
    public int ItemId { get; init; }

    /// <summary>数量（接口恒为 1，UIGF 要求显式字段）。</summary>
    public int Count { get; init; } = 1;

    public bool IsFiveStar => RankType == 5;

    public bool IsFourStar => RankType == 4;
}


/// <summary>某个卡池的统计。</summary>
public sealed record GachaPoolStatistics(
    string PoolName,
    int GachaType,
    int TotalPulls,
    int FiveStarCount,
    int FourStarCount,
    /// <summary>当前距上一发 5 星的抽数（保底进度）。</summary>
    int CurrentPity,
    /// <summary>历史平均出货抽数。</summary>
    double AveragePity,
    /// <summary>每一发 5 星的抽数记录（从旧到新）。</summary>
    IReadOnlyList<int> FiveStarPities);


/// <summary>
/// 抽卡记录服务：从游戏网页缓存取 URL、拉取记录、做统计。
/// </summary>
/// <remarks>
/// <para>
/// 见 <c>docs/05-ROADMAP.md</c> §3.1："抽卡记录 —— <c>Core/Gacha</c> 直接复用，只重写视图"。
/// 本类<b>不重写</b>任何 API 客户端，只做三件事：定位 URL、调用 Core、把结果整形。
/// </para>
/// <para>
/// 统计逻辑全部是纯函数（<see cref="ComputeStatistics"/>），便于单元测试，
/// 也避免"界面上的数字算错"这种最难发现的问题。
/// </para>
/// </remarks>
public sealed class GachaService
{
    /// <summary>各 <c>gacha_type</c> 的显示名（原神）。</summary>
    private static readonly Dictionary<int, string> GenshinPools = new()
    {
        [100] = "新手祈愿",
        [200] = "常驻祈愿",
        [301] = "角色活动祈愿",
        [302] = "武器活动祈愿",
        [500] = "集录祈愿",
    };


    private static readonly Dictionary<int, string> ZzzPools = new()
    {
        [1] = "常驻频段",
        [2] = "独家频段",
        [3] = "音擎频段",
        [5] = "邦布频段",
    };


    private readonly HttpClient _httpClient;


    public GachaService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }


    /// <summary>卡池显示名。</summary>
    public static string GetPoolName(string gameKind, int gachaType)
    {
        var table = gameKind == "zzz" ? ZzzPools : GenshinPools;
        return table.TryGetValue(gachaType, out var name) ? name : $"未知卡池({gachaType})";
    }


    /// <summary>
    /// 从游戏网页缓存里找抽卡链接。找不到时返回 <c>null</c>（并给出原因）。
    /// </summary>
    public static string? FindGachaUrl(string gameDirectory, GameBiz gameBiz, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            error = $"游戏目录不存在：{gameDirectory}";
            return null;
        }

        try
        {
            var url = GachaLogClient.GetGachaUrlFromWebCache(gameBiz, gameDirectory);
            if (string.IsNullOrEmpty(url))
            {
                error = "网页缓存里没有抽卡链接。请先在游戏里打开一次「祈愿 / 调频」页面，再回来读取。";
                return null;
            }

            return url;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }


    /// <summary>
    /// 按指定语言重新命名记录（上游 <c>GachaLogPage</c> 的「应用」→ <c>ChangeGachaItemNameCommand</c>）。
    /// </summary>
    /// <remarks>
    /// 上游先按 uid 读回全部记录、拉对应语言的 wiki 名称表、再逐条改名写库；
    /// LunarWard 的记录在页面内存 + JSON 存档里，所以这里返回改名后的列表，由调用方回写。
    /// 名称表按 <c>item_id</c> 索引（原神是 avatar/weapon 两张表的并集）。
    /// </remarks>
    public async Task<(IReadOnlyList<GachaRecord> Records, int Renamed)> ChangeItemNamesAsync(
        string gameKind,
        GameBiz gameBiz,
        string lang,
        IReadOnlyList<GachaRecord> records,
        CancellationToken ct = default)
    {
        if (records.Count == 0)
        {
            return (records, 0);
        }

        var names = new Dictionary<int, string>();
        if (gameKind == "zzz")
        {
            ZZZGachaWiki wiki = await new ZZZGachaClient(_httpClient)
                .GetZZZGachaInfoAsync(gameBiz, lang, ct)
                .ConfigureAwait(false);
            foreach (ZZZGachaInfo info in wiki.List ?? [])
            {
                if (info.Id != 0 && !string.IsNullOrWhiteSpace(info.Name))
                {
                    names[info.Id] = info.Name;
                }
            }
        }
        else if (gameKind == "genshin")
        {
            GenshinGachaWiki wiki = await new GenshinGachaClient(_httpClient)
                .GetGenshinGachaInfoAsync(gameBiz, lang, ct)
                .ConfigureAwait(false);
            foreach (GenshinGachaInfo info in (wiki.AllAvatar ?? []).Concat(wiki.AllWeapon ?? []))
            {
                if (info.Id != 0 && !string.IsNullOrWhiteSpace(info.Name))
                {
                    names[info.Id] = info.Name;
                }
            }
        }
        else
        {
            throw new NotSupportedException($"Gacha record renaming is not supported for {gameKind}.");
        }

        return ApplyItemNames(records, names);
    }


    /// <summary>
    /// LunarWard 补充（上游 <c>GenshinGachaService.GetGachaLogItemEx</c> 的 <c>IsUp</c> 计算）：
    /// 库里没有 <c>IsUp</c> 列，读出来的是「接口给的值」；这里按 <see cref="GachaNoUp"/> 的
    /// 卡池时间表重算 5★ 行的 UP 标记，4★ 与其它星级保持原值。
    /// </summary>
    public static IReadOnlyList<GachaRecord> ApplyUpFlags(string gameKind, IReadOnlyList<GachaRecord> records)
    {
        List<GachaRecord> list = new(records.Count);
        foreach (GachaRecord record in records)
        {
            if (record.RankType == 5
                && GachaNoUp.ComputeIsUp(gameKind, record.GachaType, record.ItemId, record.Time) is { } isUp)
            {
                list.Add(record with { IsUp = isUp });
            }
            else
            {
                list.Add(record);
            }
        }
        return list;
    }


    /// <summary>
    /// 按 <see cref="GachaRecord.Id"/> 合并两批记录（导入进现有列表）；纯函数。
    /// </summary>
    /// <remarks>
    /// 上游导入直接写数据库、由数据库的主键去重；这里用同一套语义：同 <c>Id</c> 以后者为准，
    /// 输出按时间倒序（与接口返回顺序一致，页面再自行分组/排序）。
    /// </remarks>
    public static IReadOnlyList<GachaRecord> MergeById(
        IReadOnlyList<GachaRecord> existing,
        IEnumerable<GachaRecord> incoming)
    {
        var byId = new Dictionary<long, GachaRecord>(existing.Count);
        foreach (GachaRecord record in existing)
        {
            byId[record.Id] = record;
        }
        foreach (GachaRecord record in incoming)
        {
            byId[record.Id] = record;
        }
        return byId.Values
            .OrderByDescending(r => r.Time)
            .ThenByDescending(r => r.Id)
            .ToList();
    }


    /// <summary>
    /// 按 <c>item_id</c> 把记录名换成名称表里的名字；纯函数，便于离线测试。
    /// </summary>
    public static (IReadOnlyList<GachaRecord> Records, int Renamed) ApplyItemNames(
        IReadOnlyList<GachaRecord> records,
        IReadOnlyDictionary<int, string> names)
    {
        var result = new List<GachaRecord>(records.Count);
        int renamed = 0;
        foreach (GachaRecord record in records)
        {
            if (names.TryGetValue(record.ItemId, out string? name)
                && !string.IsNullOrWhiteSpace(name)
                && name != record.Name)
            {
                result.Add(record with { Name = name });
                renamed++;
            }
            else
            {
                result.Add(record);
            }
        }
        return (result, renamed);
    }


    /// <summary>
    /// 拉取全部抽卡记录。
    /// </summary>
    /// <param name="gameKind"><c>genshin</c> | <c>zzz</c>。</param>
    /// <param name="gachaUrl">从 <see cref="FindGachaUrl"/> 得到的链接。</param>
    public async Task<IReadOnlyList<GachaRecord>> FetchAsync(
        string gameKind,
        string gachaUrl,
        IProgress<(string Pool, int Page)>? progress = null,
        CancellationToken ct = default)
    {
        GachaLogClient client = gameKind == "zzz"
            ? new ZZZGachaClient(_httpClient)
            : new GenshinGachaClient(_httpClient);

        var items = await client.GetGachaLogAsync(
            gachaUrl,
            endId: 0,
            lang: null,
            progress: progress is null
                ? null
                : new Progress<(IGachaType GachaType, int Page)>(p => progress.Report((GetPoolName(gameKind, p.GachaType.Value), p.Page))),
            cancellationToken: ct).ConfigureAwait(false);

        return items.Select(item => new GachaRecord
        {
            Id = item.Id,
            Name = item.Name ?? "",
            ItemType = item.ItemType ?? "",
            RankType = item.RankType,
            GachaType = item.GachaType,
            GachaTypeName = GetPoolName(gameKind, item.GachaType),
            Time = item.Time,
            Uid = item.Uid,
            IsUp = item.IsUp == 1,
            ItemId = item.ItemId,
            Count = 1,
        }).ToList();
    }


    /// <summary>
    /// 按卡池统计。纯函数：只依赖输入，不读文件、不发请求。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>保底语义</b>：游戏内的"距上一发 5 星"是从**该卡池最近一次 5 星之后**算起的抽数。
    /// 因此 <see cref="GachaPoolStatistics.CurrentPity"/> 是"最新记录往回数到最近一个 5 星之前的记录条数"；
    /// 若该池从未出过 5 星，则等于总抽数。
    /// </para>
    /// <para>
    /// 记录按时间<b>从旧到新</b>排序（Core 拉取时是倒序，这里统一正序再算）。
    /// 同一条记录可能因分页重复出现，按 <see cref="GachaRecord.Id"/> 去重。
    /// </para>
    /// </remarks>
    public static IReadOnlyList<GachaPoolStatistics> ComputeStatistics(IEnumerable<GachaRecord> records)
    {
        var result = new List<GachaPoolStatistics>();

        foreach (var group in records
                     .GroupBy(r => r.GachaType)
                     .OrderBy(g => g.Key))
        {
            // 去重 + 正序
            var ordered = group
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .OrderBy(r => r.Time)
                .ThenBy(r => r.Id)
                .ToList();

            if (ordered.Count == 0)
            {
                continue;
            }

            var fiveStarPities = new List<int>();
            var pullsSinceLastFive = 0;

            foreach (var record in ordered)
            {
                pullsSinceLastFive++;
                if (record.RankType == 5)
                {
                    fiveStarPities.Add(pullsSinceLastFive);
                    pullsSinceLastFive = 0;
                }
            }

            result.Add(new GachaPoolStatistics(
                PoolName: ordered[0].GachaTypeName,
                GachaType: group.Key,
                TotalPulls: ordered.Count,
                FiveStarCount: ordered.Count(r => r.RankType == 5),
                FourStarCount: ordered.Count(r => r.RankType == 4),
                CurrentPity: pullsSinceLastFive,
                AveragePity: fiveStarPities.Count == 0 ? 0 : Math.Round(fiveStarPities.Average(), 1),
                FiveStarPities: fiveStarPities));
        }

        return result;
    }


    /// <summary>所有 5 星记录（按时间倒序，供列表展示）。</summary>
    public static IReadOnlyList<GachaRecord> GetFiveStars(IEnumerable<GachaRecord> records, int max = 100)
        => records.Where(r => r.RankType == 5)
            .OrderByDescending(r => r.Time)
            .Take(max)
            .ToList();


    /// <summary>
    /// 从记录里找 UID（用于显示与自查询）。
    /// </summary>
    public static long? GetUid(IEnumerable<GachaRecord> records)
        => records.Select(r => r.Uid).FirstOrDefault(uid => uid > 0) is var uid && uid > 0 ? uid : null;
}
