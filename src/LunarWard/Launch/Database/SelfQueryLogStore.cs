using Dapper;
using LunarWard.Core.SelfQuery;

namespace LunarWard.Launch.Database;

/// <summary>
/// 账号记录（游戏内「数据查询」）的 SQLite 存储（Starward <c>SelfQueryService</c> 里的 SQL 部分）。
/// </summary>
/// <remarks>
/// <para>
/// 表结构、SQL 与上游一致：<c>GenshinQueryItem</c> / <c>StarRailQueryItem</c> /
/// <c>ZZZQueryItem</c>（DDL 见 <see cref="DatabaseService"/>），
/// <c>INSERT OR REPLACE</c> 按主键覆盖、月份用 <c>STRFTIME('%Y-%m', 时间列)</c> 分组、
/// <c>DateTime LIKE 'yyyy-MM%'</c> 过滤、「检索全部」先按涉及月份 DELETE 再整体重写。
/// </para>
/// <para>
/// 与原 JSON 实现的差异只有持久化方式；方法签名保持一一对应，页面 VM 不用改逻辑。
/// </para>
/// </remarks>
public sealed class SelfQueryLogStore : ISelfQueryStore
{
    private readonly LunarWardDatabase _database;


    public SelfQueryLogStore(LunarWardDatabase database) => _database = database;


    /// <summary>按数据目录打开默认库（<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>）。</summary>
    public static SelfQueryLogStore OpenDefault(string dataHome)
        => new(new LunarWardDatabase(Path.Join(dataHome, LunarWardDatabase.FileName)));


    #region Genshin


    public List<long> GetGenshinUids()
        => QueryLongs("SELECT DISTINCT Uid FROM GenshinQueryItem;");


    public (long Add, long Sub) GetGenshinQueryItemsNumSum(long uid, GenshinQueryType type)
        => QueryNumSum("GenshinQueryItem", uid, (int)type);


    public long GetGenshinLastId(long uid, GenshinQueryType type)
        => QueryLastId("GenshinQueryItem", uid, (int)type);


    public List<string> GetGenshinMonths(long uid, GenshinQueryType type)
        => QueryMonths("GenshinQueryItem", "DateTime", uid, (int)type);


    public List<GenshinQueryItem> GetGenshinQueryItems(long uid, GenshinQueryType type, string? month)
    {
        using var dapper = _database.CreateConnection();
        List<GenshinQueryItem> list = month is null
            ? dapper.Query<GenshinQueryItem>("SELECT * FROM GenshinQueryItem WHERE Uid=@uid AND Type=@type ORDER BY DateTime DESC;", new { uid, type }).ToList()
            : dapper.Query<GenshinQueryItem>("SELECT * FROM GenshinQueryItem WHERE Uid=@uid AND Type=@type AND DateTime LIKE @month ORDER BY DateTime DESC;", new { uid, type, month = month + "%" }).ToList();
        // 本地 GenshinQueryItem 有 DateTime 与 Time 两个字段（API 里叫 datetime，表里只有 DateTime），
        // 页面按 Time 显示已排序结果，这里补齐（上游页面直接用 DateTime）。
        foreach (GenshinQueryItem item in list)
        {
            item.Time = item.DateTime;
        }
        return list;
    }


    public void DeleteGenshinMonth(long uid, GenshinQueryType type, string month)
        => DeleteMonth("GenshinQueryItem", "DateTime", uid, (int)type, month);


    public void InsertGenshinQueryItems(long uid, GenshinQueryType type, List<GenshinQueryItem> items, bool all = false)
    {
        using var dapper = _database.CreateConnection();
        using var t = dapper.BeginTransaction();
        DeleteTouchedMonths(dapper, t, "GenshinQueryItem", "DateTime", uid, (int)type, items.Select(x => x.DateTime), all);
        dapper.Execute("""
            INSERT OR REPLACE INTO GenshinQueryItem (Uid, Id, AddNum, Reason, DateTime, Type, Icon, Level, Quality, Name)
            VALUES (@Uid, @Id, @AddNum, @Reason, @DateTime, @Type, @Icon, @Level, @Quality, @Name);
            """, items.Select(x => new
        {
            Uid = uid,
            x.Id,
            x.AddNum,
            Reason = x.Reason ?? "",
            DateTime = x.DateTime,
            Type = (int)type,
            Icon = x.Icon ?? "",
            x.Level,
            x.Quality,
            Name = x.Name ?? "",
        }).ToList(), t);
        t.Commit();
    }

    #endregion


    #region Star Rail


    public List<long> GetStarRailUids()
        => QueryLongs("SELECT DISTINCT Uid FROM StarRailQueryItem;");


    public (long Add, long Sub) GetStarRailQueryItemsNumSum(long uid, StarRailQueryType type)
        => QueryNumSum("StarRailQueryItem", uid, (int)type);


    public long GetStarRailLastId(long uid, StarRailQueryType type)
        => QueryLastId("StarRailQueryItem", uid, (int)type);


    public List<string> GetStarRailMonths(long uid, StarRailQueryType type)
        => QueryMonths("StarRailQueryItem", "Time", uid, (int)type);


    public List<StarRailQueryItem> GetStarRailQueryItems(long uid, StarRailQueryType type, string? month)
    {
        using var dapper = _database.CreateConnection();
        return month is null
            ? dapper.Query<StarRailQueryItem>("SELECT * FROM StarRailQueryItem WHERE Uid=@uid AND Type=@type ORDER BY Time DESC;", new { uid, type }).ToList()
            : dapper.Query<StarRailQueryItem>("SELECT * FROM StarRailQueryItem WHERE Uid=@uid AND Type=@type AND Time LIKE @month ORDER BY Time DESC;", new { uid, type, month = month + "%" }).ToList();
    }


    public void DeleteStarRailMonth(long uid, StarRailQueryType type, string month)
        => DeleteMonth("StarRailQueryItem", "Time", uid, (int)type, month);


    public void InsertStarRailQueryItems(long uid, StarRailQueryType type, List<StarRailQueryItem> items, bool all = false)
    {
        using var dapper = _database.CreateConnection();
        using var t = dapper.BeginTransaction();
        DeleteTouchedMonths(dapper, t, "StarRailQueryItem", "Time", uid, (int)type, items.Select(x => x.Time), all);
        dapper.Execute("""
            INSERT OR REPLACE INTO StarRailQueryItem (Id, Uid, Type, Action, AddNum, Time, RelicName, RelicLevel, RelicRarity, EquipmentName, EquipmentLevel, EquipmentRarity)
            VALUES (@Id, @Uid, @Type, @Action, @AddNum, @Time, @RelicName, @RelicLevel, @RelicRarity, @EquipmentName, @EquipmentLevel, @EquipmentRarity);
            """, items.Select(x => new
        {
            x.Id,
            Uid = uid,
            Type = (int)type,
            Action = x.Action ?? "",
            x.AddNum,
            x.Time,
            RelicName = x.RelicName ?? "",
            x.RelicLevel,
            x.RelicRarity,
            EquipmentName = x.EquipmentName ?? "",
            x.EquipmentLevel,
            x.EquipmentRarity,
        }).ToList(), t);
        t.Commit();
    }

    #endregion


    #region ZZZ


    public List<long> GetZZZUids()
        => QueryLongs("SELECT DISTINCT Uid FROM ZZZQueryItem;");


    public (long Add, long Sub) GetZZZQueryItemsNumSum(long uid, ZZZQueryType type)
    {
        using var dapper = _database.CreateConnection();
        if (type is ZZZQueryType.PurchaseGift)
        {
            // 上游对「购买赠礼」按条数统计（没有增减金额）。
            long count = dapper.QueryFirstOrDefault<long>("""
                SELECT COUNT(*) FROM ZZZQueryItem WHERE Uid=@uid AND Type=@type;
                """, new { uid, type = (int)type });
            return (count, 0);
        }
        long add = dapper.QueryFirstOrDefault<long>("""
            SELECT IFNULL(SUM(AddNum), 0) FROM ZZZQueryItem WHERE Uid=@uid AND Type=@type AND AddNum>0;
            """, new { uid, type = (int)type });
        long sub = dapper.QueryFirstOrDefault<long>("""
            SELECT IFNULL(SUM(AddNum), 0) FROM ZZZQueryItem WHERE Uid=@uid AND Type=@type AND AddNum<0;
            """, new { uid, type = (int)type });
        return (add, sub);
    }


    public long GetZZZLastId(long uid, ZZZQueryType type)
        => QueryLastId("ZZZQueryItem", uid, (int)type);


    public List<string> GetZZZMonths(long uid, ZZZQueryType type)
        => QueryMonths("ZZZQueryItem", "DateTime", uid, (int)type);


    public List<ZZZQueryItem> GetZZZQueryItems(long uid, ZZZQueryType type, string? month)
    {
        using var dapper = _database.CreateConnection();
        return month is null
            ? dapper.Query<ZZZQueryItem>("SELECT * FROM ZZZQueryItem WHERE Uid=@uid AND Type=@type ORDER BY DateTime DESC;", new { uid, type = (int)type }).ToList()
            : dapper.Query<ZZZQueryItem>("SELECT * FROM ZZZQueryItem WHERE Uid=@uid AND Type=@type AND DateTime LIKE @month ORDER BY DateTime DESC;", new { uid, type = (int)type, month = month + "%" }).ToList();
    }


    public void DeleteZZZMonth(long uid, ZZZQueryType type, string month)
        => DeleteMonth("ZZZQueryItem", "DateTime", uid, (int)type, month);


    public void InsertZZZQueryItems(long uid, ZZZQueryType type, List<ZZZQueryItem> items, bool all = false)
    {
        using var dapper = _database.CreateConnection();
        using var t = dapper.BeginTransaction();
        DeleteTouchedMonths(dapper, t, "ZZZQueryItem", "DateTime", uid, (int)type, items.Select(x => x.DateTime), all);
        dapper.Execute("""
            INSERT OR REPLACE INTO ZZZQueryItem (Id, Uid, Type, Reason, AddNum, DateTime, EquipName, EquipRarity, EquipLevel, WeaponName, WeaponRarity, WeaponLevel, ClientIp, ActionName, CardType, ItemName)
            VALUES (@Id, @Uid, @Type, @Reason, @AddNum, @DateTime, @EquipName, @EquipRarity, @EquipLevel, @WeaponName, @WeaponRarity, @WeaponLevel, @ClientIp, @ActionName, @CardType, @ItemName);
            """, items.Select(x => new
        {
            x.Id,
            Uid = uid,
            Type = (int)type,
            Reason = x.Reason ?? "",
            x.AddNum,
            x.DateTime,
            EquipName = x.EquipName ?? "",
            x.EquipRarity,
            x.EquipLevel,
            WeaponName = x.WeaponName ?? "",
            x.WeaponRarity,
            x.WeaponLevel,
            ClientIp = x.ClientIp ?? "",
            ActionName = x.ActionName ?? "",
            x.CardType,
            ItemName = x.ItemName ?? "",
        }).ToList(), t);
        t.Commit();
    }

    #endregion


    #region SQL helpers


    private List<long> QueryLongs(string sql)
    {
        using var dapper = _database.CreateConnection();
        return dapper.Query<long>(sql).ToList();
    }


    private (long Add, long Sub) QueryNumSum(string table, long uid, int type)
    {
        using var dapper = _database.CreateConnection();
        long add = dapper.QueryFirstOrDefault<long>($"""
            SELECT IFNULL(SUM(AddNum), 0) FROM {table} WHERE Uid=@uid AND Type=@type AND AddNum>0;
            """, new { uid, type });
        long sub = dapper.QueryFirstOrDefault<long>($"""
            SELECT IFNULL(SUM(AddNum), 0) FROM {table} WHERE Uid=@uid AND Type=@type AND AddNum<0;
            """, new { uid, type });
        return (add, sub);
    }


    private long QueryLastId(string table, long uid, int type)
    {
        using var dapper = _database.CreateConnection();
        return dapper.QueryFirstOrDefault<long>($"""
            SELECT Id FROM {table} WHERE Uid=@uid AND Type=@type ORDER BY Id DESC LIMIT 1;
            """, new { uid, type });
    }


    private List<string> QueryMonths(string table, string column, long uid, int type)
    {
        using var dapper = _database.CreateConnection();
        // 上游：SELECT DISTINCT STRFTIME('%Y-%m', {column}) FROM {table} WHERE Type=@type ORDER BY {column} DESC;
        // 这里补了 AND Uid=@uid：上游把 uid 传进参数却没用在 WHERE 里（多 UID 时月份会串），
        // 本地页面是按 UID 展示的，保留过滤。
        return dapper.Query<string>($"""
            SELECT DISTINCT STRFTIME('%Y-%m', {column}) FROM {table}
            WHERE Uid=@uid AND Type=@type ORDER BY {column} DESC;
            """, new { uid, type }).ToList();
    }


    private void DeleteMonth(string table, string column, long uid, int type, string month)
    {
        using var dapper = _database.CreateConnection();
        // 上游 DeleteCurrentMonthData：DELETE ... WHERE Uid=@uid AND Type=@Type AND {column} LIKE @time
        dapper.Execute($"""
            DELETE FROM {table} WHERE Uid=@uid AND Type=@Type AND {column} LIKE @time;
            """, new { uid, Type = type, time = month + "%" });
    }


    /// <summary>
    /// 「检索全部」时先删掉本批涉及月份的旧数据（上游对每个月单独一条 DELETE，月份按
    /// <c>yyyy-MM</c> 从本批记录的时间算出来）。
    /// </summary>
    private static void DeleteTouchedMonths(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction,
        string table, string column, long uid, int type, IEnumerable<DateTime> times, bool all)
    {
        if (!all)
        {
            return;
        }
        foreach (string month in times.Select(x => x.ToString("yyyy-MM")).Distinct())
        {
            connection.Execute($"""
                DELETE FROM {table} WHERE Uid=@uid AND Type=@type AND {column} LIKE @time;
                """, new { uid, type, time = month + "%" }, transaction);
        }
    }

    #endregion
}
