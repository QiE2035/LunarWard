using LunarWard.Core;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Launch.Database;

namespace LunarWard.Launch.Gacha;

/// <summary>
/// 千星奇域颂愿的记录 + 物品信息 + 每个 UID 的缓存 URL，全部落在
/// <see cref="GenshinBeyondGachaStore"/>（SQLite，表与 SQL 照抄上游）。
///
/// <para>统计口径逐行对齐 Starward <c>GenshinBeyondGachaService</c>：
/// 1000（常驻颂愿）按 4★ 计保底与平均，2000（活动颂愿）按 5★ 计保底与平均；
/// 3★ 的保底单独统计；列表都是「时间倒序」后再在头部插入一条 <c>Pity</c> 占位卡。</para>
/// </summary>
public sealed class GenshinBeyondGachaService
{

    private readonly GenshinBeyondGachaClient _client;

    private readonly GenshinBeyondGachaStore _store;



    /// <param name="store">已打开的库；与 <paramref name="databaseFile"/> 二选一。</param>
    /// <param name="databaseFile">数据库文件路径（默认 <c>&lt;DataHome&gt;/LunarWardDatabase.db</c> 由调用方给全路径）。</param>
    public GenshinBeyondGachaService(GenshinBeyondGachaClient? client = null, GenshinBeyondGachaStore? store = null, string? databaseFile = null)
    {
        _client = client ?? new GenshinBeyondGachaClient();
        _store = store ?? new GenshinBeyondGachaStore(new LunarWardDatabase(databaseFile ?? throw new ArgumentNullException(nameof(databaseFile), "必须提供数据库路径")));
    }



    public List<long> GetUids() => _store.GetUids();



    /// <summary>从游戏网页缓存里找抽卡链接（对应上游 <c>GetGachaLogUrlFromWebCache</c>）。</summary>
    public string? GetGachaLogUrlFromWebCache(GameBiz gameBiz, string path)
    {
        return GenshinBeyondGachaClient.GetGachaUrlFromWebCache(gameBiz, path);
    }



    /// <summary>解析 URL 里的 UID 并记住这个 URL（对应上游写入 <c>GachaLogUrl</c> 表）。</summary>
    public async Task<long> GetUidFromGachaLogUrl(string url)
    {
        var uid = await _client.GetUidByGachaUrlAsync(url);
        if (uid != 0)
        {
            _store.SetUrl(uid, url);
        }
        return uid;
    }



    public string? GetGachaLogUrlByUid(long uid) => _store.GetUrl(uid);



    /// <summary>
    /// 拉取记录并写入本地。返回 UID；0 表示该账号近 6 个月没有颂愿记录。
    /// </summary>
    /// <param name="all">true = 忽略本地已有的最大 Id，从头整页拉取（「获取全部记录」）。</param>
    public async Task<long> GetGachaLogAsync(string url, bool all, string? lang = null, IProgress<(int GachaType, int Page)>? progress = null, CancellationToken cancellationToken = default)
    {
        var uid = await _client.GetUidByGachaUrlAsync(url);
        if (uid == 0)
        {
            return 0;
        }

        var endId = all ? 0 : _store.GetLastId(uid);
        var list = (await _client.GetGachaLogAsync(url, endId, lang, progress, cancellationToken)).ToList();
        if (cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException();
        }

        _store.InsertItems(list.Select(x => new GenshinBeyondGachaItemEx
        {
            Uid = x.Uid,
            Id = x.Id,
            Region = x.Region,
            OpGachaType = x.OpGachaType,
            ScheduleId = x.ScheduleId,
            ItemType = x.ItemType,
            ItemId = x.ItemId,
            ItemName = x.ItemName,
            RankType = x.RankType,
            IsUp = x.IsUp,
            Time = x.Time,
        }));
        return uid;
    }



    /// <summary>常驻颂愿（1000）的统计卡。</summary>
    public GenshinBeyondGachaTypeStats? GetGachaTypeStatsType1000(long uid)
    {
        var list = _store.GetItems(uid, 1000);
        if (list.Count == 0)
        {
            return null;
        }

        int index = 0;
        int pity = 0;
        foreach (var item in list)
        {
            item.Index = ++index;
            item.Pity = ++pity;
            if (item.RankType == 4)
            {
                pity = 0;
            }
        }

        var stats = new GenshinBeyondGachaTypeStats
        {
            GachaType = 1000,
            GachaTypeText = "常驻颂愿",
            Count = list.Count,
            Count_5 = list.Count(x => x.RankType == 5),
            Count_4 = list.Count(x => x.RankType == 4),
            Count_3 = list.Count(x => x.RankType == 3),
            Count_2 = list.Count(x => x.RankType == 2),
            StartTime = list[0].Time,
            EndTime = list[^1].Time,
        };
        stats.Ratio_5 = (double)stats.Count_5 / stats.Count;
        stats.Ratio_4 = (double)stats.Count_4 / stats.Count;
        stats.Ratio_3 = (double)stats.Count_3 / stats.Count;
        stats.Ratio_2 = (double)stats.Count_2 / stats.Count;
        stats.List_5 = list.Where(x => x.RankType == 5).Reverse().ToList();
        stats.List_4 = list.Where(x => x.RankType == 4).Reverse().ToList();
        stats.List_3 = list.Where(x => x.RankType == 3).Reverse().ToList();

        stats.Pity_4 = list[^1].Pity;
        if (list[^1].RankType == 4)
        {
            stats.Pity_4 = 0;
        }
        stats.Average_4 = (double)(stats.Count - stats.Pity_4) / stats.Count_4;
        stats.Pity_3 = list.Count - 1 - list.FindLastIndex(x => x.RankType == 3);
        int pity_3 = 0;
        foreach (var item in list)
        {
            pity_3++;
            if (item.RankType == 3)
            {
                item.Pity = pity_3;
                pity_3 = 0;
            }
        }

        stats.List_4.Insert(0, new GenshinBeyondGachaItemEx
        {
            OpGachaType = 1000,
            ItemName = "保底",
            Pity = stats.Pity_4,
            Time = list[^1].Time,
        });
        stats.List_3.Insert(0, new GenshinBeyondGachaItemEx
        {
            OpGachaType = 1000,
            ItemName = "保底",
            Pity = stats.Pity_3,
            Time = list[^1].Time,
        });

        return stats;
    }



    /// <summary>活动颂愿（2000）的统计卡。与 1000 的差别：保底与平均按 5★ 计，第二张列表是 5★/4★。</summary>
    public GenshinBeyondGachaTypeStats? GetGachaTypeStatsType2000(long uid)
    {
        var list = _store.GetItems(uid, 2000);
        if (list.Count == 0)
        {
            return null;
        }

        int index = 0;
        int pity = 0;
        foreach (var item in list)
        {
            item.Index = ++index;
            item.Pity = ++pity;
            if (item.RankType == 5)
            {
                pity = 0;
            }
        }

        var stats = new GenshinBeyondGachaTypeStats
        {
            GachaType = 2000,
            GachaTypeText = "活动颂愿",
            Count = list.Count,
            Count_5 = list.Count(x => x.RankType == 5),
            Count_4 = list.Count(x => x.RankType == 4),
            Count_3 = list.Count(x => x.RankType == 3),
            Count_2 = list.Count(x => x.RankType == 2),
            StartTime = list[0].Time,
            EndTime = list[^1].Time,
        };
        stats.Ratio_5 = (double)stats.Count_5 / stats.Count;
        stats.Ratio_4 = (double)stats.Count_4 / stats.Count;
        stats.Ratio_3 = (double)stats.Count_3 / stats.Count;
        stats.Ratio_2 = (double)stats.Count_2 / stats.Count;
        stats.List_5 = list.Where(x => x.RankType == 5).Reverse().ToList();
        stats.List_4 = list.Where(x => x.RankType == 4).Reverse().ToList();
        stats.List_3 = list.Where(x => x.RankType == 3).Reverse().ToList();

        stats.Pity_5 = list[^1].Pity;
        if (list[^1].RankType == 5)
        {
            stats.Pity_5 = 0;
        }
        stats.Average_5 = (double)(stats.Count - stats.Pity_5) / stats.Count_5;
        stats.Pity_4 = list.Count - 1 - list.FindLastIndex(x => x.RankType == 4);
        int pity_4 = 0;
        foreach (var item in list)
        {
            pity_4++;
            if (item.RankType == 4)
            {
                item.Pity = pity_4;
                pity_4 = 0;
            }
        }

        stats.List_5.Insert(0, new GenshinBeyondGachaItemEx
        {
            OpGachaType = 2000,
            ItemName = "保底",
            Pity = stats.Pity_5,
            Time = list[^1].Time,
        });
        stats.List_4.Insert(0, new GenshinBeyondGachaItemEx
        {
            OpGachaType = 2000,
            ItemName = "保底",
            Pity = stats.Pity_4,
            Time = list[^1].Time,
        });

        return stats;
    }



    /// <summary>按物品聚合的「材料统计」：稀有度降序 → 次数降序 → 时间降序。</summary>
    public List<GenshinBeyondGachaItemEx>? GetGachaItemStats(long uid)
    {
        var list = _store.GetItems(uid);
        if (list.Count == 0)
        {
            return null;
        }
        return list.GroupBy(x => x.ItemId)
                   .Select(x =>
                   {
                       var item = x.First();
                       item.Count = x.Count();
                       return item;
                   })
                   .OrderByDescending(x => x.RankType)
                   .ThenByDescending(x => x.Count)
                   .ThenByDescending(x => x.Time)
                   .ToList();
    }



    public int DeleteUid(long uid) => _store.DeleteUid(uid);



    public int DeleteGachaLogByTime(long uid, DateTime begin, DateTime end) => _store.DeleteByTime(uid, begin, end);



    public async Task UpdateGachaInfoAsync(CancellationToken cancellationToken = default)
    {
        var data = await _client.GetGenshinBeyondGachaInfoAsync(cancellationToken);
        _store.ReplaceInfo(data);
    }

}



/// <summary>记录 + 聚合用的附加字段（对应上游 <c>GenshinBeyondGachaItemEx</c>）。</summary>
public partial class GenshinBeyondGachaItemEx : GenshinBeyondGachaItem
{

    /// <summary>该记录在统计序列里的序号（从 1 开始）。</summary>
    public int Index { get; set; }

    /// <summary>截至这一抽的保底计数。</summary>
    public int Pity { get; set; }

    /// <summary>图标 URL，来自 <c>GenshinBeyondGachaInfo</c> 的关联查询。</summary>
    public string Icon { get; set; } = "";

    /// <summary>同物品出现次数（材料统计用）。</summary>
    public int Count { get; set; }

}



/// <summary>一张统计卡的数据（对应上游 <c>GenshinBeyondGachaTypeStats</c>）。</summary>
public class GenshinBeyondGachaTypeStats
{

    public int GachaType { get; set; }

    public string GachaTypeText { get; set; } = "";

    /// <summary>该类型的总抽数。</summary>
    public int Count { get; set; }

    /// <summary>距离上一个 4★ 的抽数（类型 1000 用）。</summary>
    public int Pity_4 { get; set; }

    /// <summary>距离上一个 5★ 的抽数（类型 2000 用）。</summary>
    public int Pity_5 { get; set; }

    /// <summary>距离上一个 3★ 的抽数。</summary>
    public int Pity_3 { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Count_5 { get; set; }

    public int Count_4 { get; set; }

    public int Count_3 { get; set; }

    public int Count_2 { get; set; }

    public double Ratio_5 { get; set; }

    public double Ratio_4 { get; set; }

    public double Ratio_3 { get; set; }

    public double Ratio_2 { get; set; }

    public double Average_5 { get; set; }

    public double Average_4 { get; set; }

    public List<GenshinBeyondGachaItemEx> List_5 { get; set; } = [];

    public List<GenshinBeyondGachaItemEx> List_4 { get; set; } = [];

    public List<GenshinBeyondGachaItemEx> List_3 { get; set; } = [];

}
