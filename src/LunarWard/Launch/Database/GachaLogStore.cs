using System.Globalization;
using Dapper;

namespace LunarWard.Launch.Database;

/// <summary>
/// 抽卡记录的读写（Starward <c>Features/Gacha/GachaLogService</c> 及三个子类里与存储相关的部分）。
/// </summary>
/// <remarks>
/// <para>
/// 表结构、SQL 与上游一致：<c>INSERT OR REPLACE</c> 按 <c>(Uid, Id)</c> 覆盖、
/// <c>ORDER BY Id</c> 升序读出、<c>DELETE ... WHERE Uid=@uid AND Time BETWEEN</c> 删除时间段。
/// 语言存在每行的 <c>Lang</c> 列（上游同款），不是存档级字段。
/// </para>
/// <para>
/// 上游记录里没有 <c>IsUp</c> 列：<c>up!</c> 是读库后用 <c>GachaNoUp</c> 表算出来的。
/// LunarWard 目前还没有这张表，所以从库里读出的 <see cref="GachaRecord.IsUp"/> 一律为
/// <see langword="false"/>（待移植 <c>GachaNoUp</c> 后再补）。
/// </para>
/// </remarks>
public sealed class GachaLogStore
{
    /// <summary>原神（UIGF v4.2 的 <c>hk4e</c> 区段）。</summary>
    public const string Genshin = "genshin";

    /// <summary>星穹铁道（<c>hkrpg</c>）。</summary>
    public const string StarRail = "starrail";

    /// <summary>绝区零（<c>nap</c>）。</summary>
    public const string Zzz = "zzz";


    /// <summary>导出 / 导入列表的游戏顺序（与上游 <c>GetLocalGachaArchives</c> 一致）。</summary>
    public static readonly string[] GameOrder = [Genshin, StarRail, Zzz];


    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";


    private readonly LunarWardDatabase _database;


    public GachaLogStore(LunarWardDatabase database) => _database = database;


    /// <summary>按数据目录打开默认库（<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>）。</summary>
    public static GachaLogStore OpenDefault(string dataHome)
        => new(new LunarWardDatabase(Path.Join(dataHome, LunarWardDatabase.FileName)));


    /// <summary>Starward 的表名（<c>GachaTableName</c>）。</summary>
    public static string TableName(string game) => game switch
    {
        StarRail => "StarRailGachaItem",
        Zzz => "ZZZGachaItem",
        _ => "GenshinGachaItem",
    };


    /// <summary>
    /// 写入记录（上游 <c>InsertGachaLogItems</c>：<c>INSERT OR REPLACE</c>，同 <c>(Uid, Id)</c> 覆盖）。
    /// </summary>
    /// <returns>写入条数。</returns>
    public int Merge(string game, IEnumerable<GachaRecord> records, string? lang = null)
    {
        List<GachaRecord> list = records.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        string table = TableName(game);
        using var dapper = _database.CreateConnection();
        using var t = dapper.BeginTransaction();
        var items = list.Select(x => new
        {
            x.Uid,
            x.Id,
            x.Name,
            Time = x.Time,
            x.ItemId,
            x.ItemType,
            x.RankType,
            x.GachaType,
            GachaId = game == StarRail ? x.GachaId : 0,
            x.Count,
            Lang = lang,
        });
        int affect = game == StarRail
            ? dapper.Execute("""
                INSERT OR REPLACE INTO StarRailGachaItem (Uid, Id, Name, Time, ItemId, ItemType, RankType, GachaType, GachaId, Count, Lang)
                VALUES (@Uid, @Id, @Name, @Time, @ItemId, @ItemType, @RankType, @GachaType, @GachaId, @Count, @Lang);
                """, items, t)
            : dapper.Execute($"""
                INSERT OR REPLACE INTO {table} (Uid, Id, Name, Time, ItemId, ItemType, RankType, GachaType, Count, Lang)
                VALUES (@Uid, @Id, @Name, @Time, @ItemId, @ItemType, @RankType, @GachaType, @Count, @Lang);
                """, items, t);
        t.Commit();
        return affect;
    }


    /// <summary>重命名记录（上游 <c>ChangeGachaItemNameAsync</c>：改名并写回 <c>Lang</c>）。</summary>
    /// <remarks>
    /// 上游按 <c>INNER JOIN {info} ON item.ItemId = info.Id</c> 整表改名；LunarWard 的调用方
    /// （<c>GachaPageViewModel</c>）是先取新语言的名字、再带着这批记录来改，
    /// 而且信息表要到之后的 <c>RefreshWikiDataAsync</c> 才刷新，所以这里保留逐行 UPDATE。
    /// </remarks>
    public int Rename(string game, string lang, IEnumerable<GachaRecord> records)
    {
        string table = TableName(game);
        using var dapper = _database.CreateConnection();
        using var t = dapper.BeginTransaction();
        int affected = dapper.Execute($"UPDATE {table} SET Name = @Name, Lang = @Lang WHERE Uid = @Uid AND Id = @Id;",
            records.Select(x => new { x.Name, Lang = lang, x.Uid, x.Id }).ToList(), t);
        t.Commit();
        return affected;
    }


    /// <summary>某 UID 的全部记录，按 <c>Id</c> 升序（上游 <c>ORDER BY Id</c>）。</summary>
    public IReadOnlyList<GachaRecord> GetItems(string game, long uid)
    {
        using var dapper = _database.CreateConnection();
        return Map(game, dapper.Query<GachaRow>($"SELECT * FROM {TableName(game)} WHERE Uid = @uid ORDER BY Id;", new { uid }));
    }


    /// <summary>某游戏的全部记录（跨 UID，按 Uid、Id 升序；上游没有对应查询，本地导出列表用）。</summary>
    public IReadOnlyList<GachaRecord> GetAll(string game)
    {
        using var dapper = _database.CreateConnection();
        return Map(game, dapper.Query<GachaRow>($"SELECT * FROM {TableName(game)} ORDER BY Uid, Id;"));
    }


    /// <summary>有记录的 UID 列表（上游 <c>GetUids</c>：<c>SELECT DISTINCT Uid</c>，无 ORDER BY）。</summary>
    public IReadOnlyList<long> GetUids(string game)
    {
        using var dapper = _database.CreateConnection();
        return dapper.Query<long>($"SELECT DISTINCT Uid FROM {TableName(game)};").OrderBy(x => x).ToList();
    }


    /// <summary>
    /// 全部存档摘要（游戏顺序 <see cref="GameOrder"/>，同游戏内按 UID 升序），
    /// 用于 UIGF 导出列表与导入结果列表。
    /// </summary>
    /// <remarks>
    /// 三条查询与上游 <c>UIGFGachaService.GetLocalGachaArchives</c> 对应：
    /// <c>SELECT DISTINCT Uid</c>、<c>SELECT COUNT(*)</c>、最近一条按 <c>ORDER BY Time DESC LIMIT 1</c>。
    /// </remarks>
    public IReadOnlyList<GachaArchiveSummary> GetSummaries()
    {
        var result = new List<GachaArchiveSummary>();
        foreach (string game in GameOrder)
        {
            string table = TableName(game);
            using var dapper = _database.CreateConnection();
            foreach (long uid in dapper.Query<long>($"SELECT DISTINCT Uid FROM {table};").OrderBy(x => x))
            {
                int count = dapper.QueryFirstOrDefault<int>($"SELECT COUNT(*) FROM {table} WHERE Uid=@Uid;", new { Uid = uid });
                GachaLastRow? last = dapper.QueryFirstOrDefault<GachaLastRow>($"SELECT * FROM {table} WHERE Uid=@Uid ORDER BY Time DESC LIMIT 1;", new { Uid = uid });
                if (last is null)
                {
                    continue;
                }
                result.Add(new GachaArchiveSummary(game, uid, count, last.Lang, ParseTime(last.Time), last.Name ?? "", last.GachaType));
            }
        }
        return result;
    }


    /// <summary>某 UID 在某游戏下最近一次使用的语言（没有记录则为 <see langword="null"/>）。</summary>
    public string? GetLang(string game, long uid)
    {
        using var dapper = _database.CreateConnection();
        return dapper.QueryFirstOrDefault<string?>($"SELECT Lang FROM {TableName(game)} WHERE Uid=@Uid AND Lang IS NOT NULL AND Lang <> '' ORDER BY Id DESC LIMIT 1;", new { Uid = uid });
    }


    /// <summary>按 UID + 时间段删除（Starward <c>DeleteGachaLogByTime</c>，参数直接传 <see cref="DateTime"/>）。</summary>
    public int DeleteRange(string game, long uid, DateTime begin, DateTime end)
    {
        using var dapper = _database.CreateConnection();
        return dapper.Execute($"DELETE FROM {TableName(game)} WHERE Uid = @uid AND Time >= @begin AND Time <= @end;", new { uid, begin, end });
    }


    /// <summary>删除某 UID 的全部记录（Starward <c>DeleteUid</c>）。</summary>
    public int DeleteUid(string game, long uid)
    {
        using var dapper = _database.CreateConnection();
        return dapper.Execute($"DELETE FROM {TableName(game)} WHERE Uid = @uid;", new { uid });
    }


    /// <summary>
    /// 写入抽卡物品信息（Starward <c>UpdateGachaInfoAsync</c>：<c>INSERT OR REPLACE</c> 进
    /// <c>GenshinGachaInfo</c> / <c>StarRailGachaInfo</c> / <c>ZZZGachaInfo</c>）。
    /// </summary>
    /// <remarks>
    /// 三张表的列名不同（星铁是 <c>ItemId/ItemName/IconUrl</c>），SQL 与上游逐字一致；
    /// 上游会从 wiki 接口填 Element/Rarity 等展示字段，LunarWard 只用到 Name/Icon，其余写 0。
    /// </remarks>
    public int UpsertGachaInfo(string game, IReadOnlyList<GachaInfoItem> items)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        string sql = game switch
        {
            StarRail => """
                INSERT OR REPLACE INTO StarRailGachaInfo (ItemId, ItemName, IconUrl, DamageType, Rarity, AvatarBaseType, WikiUrl, IsSystem)
                VALUES (@ItemId, @ItemName, @IconUrl, @DamageType, @Rarity, @AvatarBaseType, @WikiUrl, @IsSystem);
                """,
            Zzz => """
                INSERT OR REPLACE INTO ZZZGachaInfo (Id, Name, Icon, Rarity, ElementType, Profession)
                VALUES (@Id, @Name, @Icon, @Rarity, @ElementType, @Profession);
                """,
            _ => """
                INSERT OR REPLACE INTO GenshinGachaInfo (Id, Name, Icon, Element, Level, CatId, WeaponCatId)
                VALUES (@Id, @Name, @Icon, @Element, @Level, @CatId, @WeaponCatId);
                """,
        };

        using var dapper = _database.CreateConnection();
        using var t = dapper.BeginTransaction();
        int affect = game switch
        {
            StarRail => dapper.Execute(sql, items.Select(x => new
            {
                ItemId = x.Id,
                ItemName = x.Name,
                IconUrl = x.Icon,
                DamageType = 0,
                Rarity = 0,
                AvatarBaseType = 0,
                WikiUrl = (string?)null,
                IsSystem = 0,
            }).ToList(), t),
            _ => dapper.Execute(sql, items.Select(x => new
            {
                x.Id,
                x.Name,
                Icon = x.Icon,
                Element = 0,
                Level = 0,
                CatId = 0,
                WeaponCatId = 0,
                Rarity = 0,
                ElementType = 0,
                Profession = 0,
            }).ToList(), t),
        };
        t.Commit();
        return affect;
    }


    /// <summary>读物品图标：<c>ItemId → Icon URL</c>（库里没有就给空字典）。</summary>
    public Dictionary<int, string> GetIconMap(string game)
    {
        string sql = game switch
        {
            StarRail => "SELECT ItemId AS Id, IconUrl AS Icon FROM StarRailGachaInfo WHERE IconUrl IS NOT NULL AND IconUrl <> '';",
            _ => "SELECT Id, Icon FROM " + (game == Zzz ? "ZZZGachaInfo" : "GenshinGachaInfo") + " WHERE Icon IS NOT NULL AND Icon <> '';",
        };

        using var dapper = _database.CreateConnection();
        var result = new Dictionary<int, string>();
        foreach (IconRow row in dapper.Query<IconRow>(sql))
        {
            result[row.Id] = row.Icon;
        }
        return result;
    }


    /// <summary>
    /// 按名字回填 <c>ItemId</c>（Starward <c>UpdateGachaItemId</c>：老记录 <c>ItemId=0</c> 时用
    /// GachaInfo 表按 Name 匹配补齐，图标和 UP 判定都靠它）。
    /// </summary>
    /// <remarks>
    /// 原神 / 星铁与上游逐字一致（<c>INSERT OR REPLACE ... SELECT ... INNER JOIN</c>）。
    /// 绝区零上游没有这一步（接口本来就给 <c>item_id</c>）；这里补一条同形态的语句，
    /// 免得本地旧库里 <c>ItemId=0</c> 的 ZZZ 记录永远没有图标。
    /// </remarks>
    public int UpdateItemIds(string game)
    {
        using var dapper = _database.CreateConnection();
        return game switch
        {
            StarRail => dapper.Execute("""
                INSERT OR REPLACE INTO StarRailGachaItem (Uid, Id, Name, Time, ItemId, ItemType, RankType, GachaType, GachaId, Count, Lang)
                SELECT item.Uid, Id, Name, Time, info.ItemId, ItemType, RankType, GachaType, GachaId, Count, Lang
                FROM StarRailGachaItem item INNER JOIN StarRailGachaInfo info ON item.Name = info.ItemName
                WHERE item.ItemId = 0;
                """),
            Zzz => dapper.Execute("""
                INSERT OR REPLACE INTO ZZZGachaItem (Uid, Id, Name, Time, ItemId, ItemType, RankType, GachaType, Count, Lang)
                SELECT item.Uid, item.Id, info.Name, Time, info.Id, ItemType, RankType, GachaType, Count, Lang
                FROM ZZZGachaItem item INNER JOIN ZZZGachaInfo info ON item.Name = info.Name
                WHERE item.ItemId = 0;
                """),
            _ => dapper.Execute("""
                INSERT OR REPLACE INTO GenshinGachaItem (Uid, Id, Name, Time, ItemId, ItemType, RankType, GachaType, Count, Lang)
                SELECT item.Uid, item.Id, item.Name, Time, info.Id, ItemType, RankType, GachaType, Count, Lang
                FROM GenshinGachaItem item INNER JOIN GenshinGachaInfo info ON item.Name = info.Name
                WHERE item.ItemId = 0;
                """),
        };
    }


    /// <summary>
    /// 读取某游戏最近一次保存的抽卡链接（Starward <c>GachaLogUrl</c> 表）。
    /// </summary>
    /// <remarks>
    /// 上游按 <c>(GameBiz, Uid)</c> 取（<c>GetGachaLogUrlByUid</c>）；LunarWard 的页面只按区服取
    /// 「上次用的链接」，所以这里没有 Uid 条件（表主键仍是 <c>(GameBiz, Uid)</c>）。
    /// </remarks>
    public string? GetGachaLogUrl(string gameBiz)
    {
        using var dapper = _database.CreateConnection();
        return dapper.QueryFirstOrDefault<string?>("SELECT Url FROM GachaLogUrl WHERE GameBiz = @GameBiz LIMIT 1;", new { GameBiz = gameBiz });
    }


    /// <summary>保存抽卡链接（Starward <c>GetUidFromGachaLogUrl</c> 里的 <c>INSERT OR REPLACE</c>）。</summary>
    public void SaveGachaLogUrl(string gameBiz, long uid, string url)
    {
        using var dapper = _database.CreateConnection();
        dapper.Execute("INSERT OR REPLACE INTO GachaLogUrl (GameBiz, Uid, Url, Time) VALUES (@GameBiz, @Uid, @Url, @Time);",
            new { GameBiz = gameBiz, Uid = uid, Url = url, Time = DateTime.Now });
    }


    private static IReadOnlyList<GachaRecord> Map(string game, IEnumerable<GachaRow> rows)
    {
        return rows.Select(row => new GachaRecord
        {
            Uid = row.Uid,
            Id = row.Id,
            Name = row.Name,
            Time = ParseTime(row.Time),
            ItemId = row.ItemId,
            ItemType = row.ItemType,
            RankType = row.RankType,
            GachaType = row.GachaType,
            GachaTypeName = GachaService.GetPoolName(game, row.GachaType),
            Count = row.Count,
            GachaId = row.GachaId,
            IsUp = false,
        }).ToList();
    }


    private static DateTime ParseTime(string text)
        => DateTime.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime exact)
            ? exact
            : DateTime.Parse(text, CultureInfo.InvariantCulture);


    /// <summary>抽卡记录表的行（列名与上游 <c>GachaLogItem</c> 一致；<c>Time</c> 按 TEXT 读再解析）。</summary>
    private sealed class GachaRow
    {
        public long Uid { get; set; }

        public long Id { get; set; }

        public string Name { get; set; } = "";

        public string Time { get; set; } = "";

        public int ItemId { get; set; }

        public string ItemType { get; set; } = "";

        public int RankType { get; set; }

        public int GachaType { get; set; }

        /// <summary>星铁 SRGF 的 <c>gacha_id</c>；其它游戏的表没有这一列，保持 0。</summary>
        public int GachaId { get; set; }

        public int Count { get; set; }

        public string? Lang { get; set; }
    }


    private sealed class GachaLastRow
    {
        public string? Name { get; set; }

        public string Time { get; set; } = "";

        public int GachaType { get; set; }

        public string? Lang { get; set; }
    }


    private sealed class IconRow
    {
        public int Id { get; set; }

        public string Icon { get; set; } = "";
    }
}


/// <summary>存档摘要（导出列表的一行 = 一个「游戏 + UID」）。</summary>
public sealed record GachaArchiveSummary(
    string Game,
    long Uid,
    int Count,
    string? Lang,
    DateTime LastItemTime,
    string LastItemName,
    int LastItemGachaType);


/// <summary>抽卡物品信息表的一行（wiki 接口的 Name + Icon）。</summary>
public sealed record GachaInfoItem(int Id, string Name, string Icon);


/// <summary>导出时的一条记录（字段与上游 <c>GachaLogItem</c> 的表列一致）。</summary>
public sealed class GachaArchiveItem
{
    public long Id { get; set; }

    public long Uid { get; set; }

    public int GachaType { get; set; }

    public int ItemId { get; set; }

    /// <summary>星铁 SRGF 的 <c>gacha_id</c>（其它游戏恒为 0）。</summary>
    public int GachaId { get; set; }

    /// <summary>千星奇域：服务器区服（<c>cn_gf01</c> 等）。</summary>
    public string Region { get; set; } = "";

    /// <summary>千星奇域：活动期次 id。</summary>
    public int ScheduleId { get; set; }

    /// <summary>千星奇域：是否 UP（<c>is_up</c>）。</summary>
    public int IsUp { get; set; }

    public int Count { get; set; } = 1;

    /// <summary>服务器时区的时间（导出时原样写进 UIGF 的 <c>time</c>）。</summary>
    public DateTime Time { get; set; }

    public string Name { get; set; } = "";

    public string ItemType { get; set; } = "";

    public int RankType { get; set; }
}
