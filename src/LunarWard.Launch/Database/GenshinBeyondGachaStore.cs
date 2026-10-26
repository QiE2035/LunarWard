using Dapper;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Launch.Gacha;

namespace LunarWard.Launch.Database;

/// <summary>
/// 千星奇域颂愿（<c>hk4eugc</c>）抽卡记录的本地持久化。
/// </summary>
/// <remarks>
/// <para>
/// 表与 SQL 照抄 Starward <c>Features/Gacha/GenshinBeyondGachaService</c>：
/// 记录在 <c>GenshinBeyondGachaItem</c>（主键 <c>(Uid, Id)</c>）、物品图标在
/// <c>GenshinBeyondGachaInfo</c>（主键 <c>Id</c>）、每个 UID 最近一次可用 URL 在
/// <c>GachaLogUrl</c>（<c>GameBiz = 'hk4eugc'</c>）。
/// 读记录时用 <c>LEFT JOIN ... ON item.ItemId = info.Id</c> 取 <c>Icon</c>，与上游一致。
/// </para>
/// <para>
/// 与标题说的"JSON 换实现"相反：这是把此前的 JSON 文件存储改回上游的 SQLite 实现。
/// </para>
/// </remarks>
public sealed class GenshinBeyondGachaStore
{

    private const string GachaTableName = "GenshinBeyondGachaItem";

    private const string GameBizUgc = "hk4eugc";


    private readonly LunarWardDatabase _database;


    public GenshinBeyondGachaStore(LunarWardDatabase database) => _database = database;


    /// <summary>按数据目录打开默认库（<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>）。</summary>
    public static GenshinBeyondGachaStore OpenDefault(string dataHome)
        => new(new LunarWardDatabase(Path.Join(dataHome, LunarWardDatabase.FileName)));


    // ── 记录 ────────────────────────────────────────────────────────────


    public List<long> GetUids()
    {
        using var dapper = _database.CreateConnection();
        // 上游 GetUids：SELECT DISTINCT Uid FROM GenshinBeyondGachaItem（无 ORDER BY）
        return dapper.Query<long>($"SELECT DISTINCT Uid FROM {GachaTableName};").OrderBy(x => x).ToList();
    }


    /// <summary>该 UID 的全部记录，按 <c>Id</c> 升序（上游 <c>ORDER BY item.Id</c>，最早在前）。</summary>
    public List<GenshinBeyondGachaItemEx> GetItems(long uid)
    {
        return Query($"""
            SELECT item.Uid, item.Id, item.Region, item.OpGachaType, item.ScheduleId, item.ItemType,
                   item.ItemId, item.ItemName, item.RankType, item.IsUp, item.Time, info.Icon
            FROM {GachaTableName} item
            LEFT JOIN GenshinBeyondGachaInfo info ON item.ItemId = info.Id
            WHERE item.Uid = @Uid
            ORDER BY item.Id;
            """, uid);
    }


    /// <summary>该 UID 在某一颂愿类型下的记录（1000 = 常驻，其它 = 活动，与上游 SQL 同义）。</summary>
    public List<GenshinBeyondGachaItemEx> GetItems(long uid, int gachaType)
    {
        return Query($"""
            SELECT item.Uid, item.Id, item.Region, item.OpGachaType, item.ScheduleId, item.ItemType,
                   item.ItemId, item.ItemName, item.RankType, item.IsUp, item.Time, info.Icon
            FROM {GachaTableName} item
            LEFT JOIN GenshinBeyondGachaInfo info ON item.ItemId = info.Id
            WHERE item.Uid = @Uid AND (item.OpGachaType {(gachaType == 1000 ? "=" : "!=")} 1000)
            ORDER BY item.Id;
            """, uid);
    }


    public int CountItems(long uid)
    {
        using var dapper = _database.CreateConnection();
        return dapper.QueryFirstOrDefault<int>($"SELECT COUNT(*) FROM {GachaTableName} WHERE Uid = @Uid;", new { Uid = uid });
    }


    public long GetLastId(long uid)
    {
        using var dapper = _database.CreateConnection();
        return dapper.QueryFirstOrDefault<long>($"SELECT Id FROM {GachaTableName} WHERE Uid = @Uid ORDER BY Id DESC LIMIT 1;", new { Uid = uid });
    }


    /// <summary>写入记录（上游 <c>InsertGachaLogItems</c>：<c>INSERT OR REPLACE</c>，同 <c>(Uid, Id)</c> 覆盖）。</summary>
    public int InsertItems(IEnumerable<GenshinBeyondGachaItemEx> items)
    {
        List<GenshinBeyondGachaItemEx> list = items.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        using var dapper = _database.CreateConnection();
        using var t = dapper.BeginTransaction();
        int affect = dapper.Execute("""
            INSERT OR REPLACE INTO GenshinBeyondGachaItem (Uid, Id, Region, OpGachaType, ScheduleId, ItemType, ItemId, ItemName, RankType, IsUp, Time)
            VALUES (@Uid, @Id, @Region, @OpGachaType, @ScheduleId, @ItemType, @ItemId, @ItemName, @RankType, @IsUp, @Time);
            """, list, t);
        t.Commit();
        return affect;
    }


    public int DeleteUid(long uid)
    {
        using var dapper = _database.CreateConnection();
        // 上游 DeleteUid：DELETE FROM 表 WHERE Uid = @uid
        return dapper.Execute($"DELETE FROM {GachaTableName} WHERE Uid = @uid;", new { uid });
    }


    public int DeleteByTime(long uid, DateTime begin, DateTime end)
    {
        using var dapper = _database.CreateConnection();
        // 上游 DeleteByTime：DELETE FROM 表 WHERE Uid = @uid AND Time >= @begin AND Time <= @end
        return dapper.Execute($"DELETE FROM {GachaTableName} WHERE Uid = @uid AND Time >= @begin AND Time <= @end;", new { uid, begin, end });
    }


    // ── 物品图标（GenshinBeyondGachaInfo） ──────────────────────────────


    public string? GetIcon(int itemId)
    {
        using var dapper = _database.CreateConnection();
        return dapper.QueryFirstOrDefault<string?>("SELECT Icon FROM GenshinBeyondGachaInfo WHERE Id = @Id LIMIT 1;", new { Id = itemId });
    }


    /// <summary>整表替换物品图标（上游 <c>UpdateGachaInfoAsync</c> 的 <c>INSERT OR REPLACE</c>）。</summary>
    public int ReplaceInfo(IEnumerable<GenshinBeyondGachaInfo> info)
    {
        List<GenshinBeyondGachaInfo> list = info.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        using var dapper = _database.CreateConnection();
        using var t = dapper.BeginTransaction();
        int affect = dapper.Execute("""
            INSERT OR REPLACE INTO GenshinBeyondGachaInfo (Id, Name, Rank, Icon) VALUES (@Id, @Name, @Rank, @Icon);
            """, list, t);
        t.Commit();
        return affect;
    }


    // ── 缓存 URL（GachaLogUrl，GameBiz = hk4eugc） ─────────────────────


    public string? GetUrl(long uid)
    {
        using var dapper = _database.CreateConnection();
        return dapper.QueryFirstOrDefault<string?>("SELECT Url FROM GachaLogUrl WHERE Uid = @uid AND GameBiz = @GameBiz LIMIT 1;",
            new { uid, GameBiz = GameBizUgc });
    }


    public void SetUrl(long uid, string url)
    {
        using var dapper = _database.CreateConnection();
        // 上游：INSERT OR REPLACE INTO GachaLogUrl (GameBiz, Uid, Url, Time)
        dapper.Execute("INSERT OR REPLACE INTO GachaLogUrl (GameBiz, Uid, Url, Time) VALUES (@GameBiz, @Uid, @Url, @Time);",
            new { GameBiz = GameBizUgc, Uid = uid, Url = url, Time = DateTime.Now });
    }


    private List<GenshinBeyondGachaItemEx> Query(string sql, long uid)
    {
        using var dapper = _database.CreateConnection();
        return dapper.Query<GenshinBeyondGachaItemEx>(sql, new { Uid = uid }).ToList();
    }

}
