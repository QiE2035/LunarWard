using System.Globalization;
using Microsoft.Data.Sqlite;

namespace LunarWard.Launch.Database;

/// <summary>
/// 抽卡记录的读写（Starward <c>Features/Gacha/GachaLogService</c> 及三个子类里与存储相关的部分）。
/// </summary>
/// <remarks>
/// <para>
/// 表结构、SQL 语义与上游一致：<c>INSERT OR REPLACE</c> 按 <c>(Uid, Id)</c> 覆盖、
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
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = game == StarRail
            ? $"""
               INSERT OR REPLACE INTO {table} (Uid, Id, Name, Time, ItemId, ItemType, RankType, GachaType, GachaId, Count, Lang)
               VALUES (@Uid, @Id, @Name, @Time, @ItemId, @ItemType, @RankType, @GachaType, @GachaId, @Count, @Lang);
               """
            : $"""
               INSERT OR REPLACE INTO {table} (Uid, Id, Name, Time, ItemId, ItemType, RankType, GachaType, Count, Lang)
               VALUES (@Uid, @Id, @Name, @Time, @ItemId, @ItemType, @RankType, @GachaType, @Count, @Lang);
               """;

        SqliteParameter pUid = command.Parameters.Add("@Uid", SqliteType.Integer);
        SqliteParameter pId = command.Parameters.Add("@Id", SqliteType.Integer);
        SqliteParameter pName = command.Parameters.Add("@Name", SqliteType.Text);
        SqliteParameter pTime = command.Parameters.Add("@Time", SqliteType.Text);
        SqliteParameter pItemId = command.Parameters.Add("@ItemId", SqliteType.Integer);
        SqliteParameter pItemType = command.Parameters.Add("@ItemType", SqliteType.Text);
        SqliteParameter pRankType = command.Parameters.Add("@RankType", SqliteType.Integer);
        SqliteParameter pGachaType = command.Parameters.Add("@GachaType", SqliteType.Integer);
        SqliteParameter pCount = command.Parameters.Add("@Count", SqliteType.Integer);
        SqliteParameter pLang = command.Parameters.Add("@Lang", SqliteType.Text);
        SqliteParameter? pGachaId = game == StarRail ? command.Parameters.Add("@GachaId", SqliteType.Integer) : null;

        foreach (GachaRecord record in list)
        {
            pUid.Value = record.Uid;
            pId.Value = record.Id;
            pName.Value = record.Name;
            pTime.Value = record.Time.ToString(TimeFormat, CultureInfo.InvariantCulture);
            pItemId.Value = record.ItemId;
            pItemType.Value = record.ItemType;
            pRankType.Value = record.RankType;
            pGachaType.Value = record.GachaType;
            pGachaId?.Value = record.GachaId;
            pCount.Value = record.Count;
            pLang.Value = (object?)lang ?? DBNull.Value;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        return list.Count;
    }


    /// <summary>重命名记录（上游 <c>ChangeGachaItemNameAsync</c>：改名并写回 <c>Lang</c>）。</summary>
    public int Rename(string game, string lang, IEnumerable<GachaRecord> records)
    {
        string table = TableName(game);
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"UPDATE {table} SET Name = @Name, Lang = @Lang WHERE Uid = @Uid AND Id = @Id;";
        SqliteParameter pName = command.Parameters.Add("@Name", SqliteType.Text);
        SqliteParameter pLang = command.Parameters.Add("@Lang", SqliteType.Text);
        SqliteParameter pUid = command.Parameters.Add("@Uid", SqliteType.Integer);
        SqliteParameter pId = command.Parameters.Add("@Id", SqliteType.Integer);

        int affected = 0;
        foreach (GachaRecord record in records)
        {
            pName.Value = record.Name;
            pLang.Value = lang;
            pUid.Value = record.Uid;
            pId.Value = record.Id;
            affected += command.ExecuteNonQuery();
        }

        transaction.Commit();
        return affected;
    }


    /// <summary>某 UID 的全部记录，按 <c>Id</c> 升序（上游 <c>ORDER BY Id</c>）。</summary>
    public IReadOnlyList<GachaRecord> GetItems(string game, long uid)
    {
        string table = TableName(game);
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {table} WHERE Uid = @uid ORDER BY Id;";
        command.Parameters.AddWithValue("@uid", uid);
        return ReadRecords(game, command);
    }


    /// <summary>某游戏的全部记录（跨 UID，按 Uid、Id 升序）。</summary>
    public IReadOnlyList<GachaRecord> GetAll(string game)
    {
        string table = TableName(game);
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {table} ORDER BY Uid, Id;";
        return ReadRecords(game, command);
    }


    /// <summary>有记录的 UID 列表（上游 <c>GetUids</c>：<c>SELECT DISTINCT Uid</c>）。</summary>
    public IReadOnlyList<long> GetUids(string game)
    {
        string table = TableName(game);
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT DISTINCT Uid FROM {table} ORDER BY Uid;";
        using SqliteDataReader reader = command.ExecuteReader();
        var uids = new List<long>();
        while (reader.Read())
        {
            uids.Add(reader.GetInt64(0));
        }
        return uids;
    }


    /// <summary>
    /// 全部存档摘要（游戏顺序 <see cref="GameOrder"/>，同游戏内按 UID 升序），
    /// 用于 UIGF 导出列表与导入结果列表。
    /// </summary>
    public IReadOnlyList<GachaArchiveSummary> GetSummaries()
    {
        var result = new List<GachaArchiveSummary>();
        foreach (string game in GameOrder)
        {
            string table = TableName(game);
            using SqliteConnection connection = _database.CreateConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT Uid, COUNT(*) AS Cnt, MAX(Time) AS LastTime
                FROM {table}
                GROUP BY Uid
                ORDER BY Uid;
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            var rows = new List<(long Uid, int Count, DateTime LastTime)>();
            while (reader.Read())
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.GetInt32(1),
                    ParseTime(reader.GetString(2))));
            }
            reader.Close();

            foreach ((long uid, int count, DateTime lastTime) in rows)
            {
                // 最近一条记录的物品名 / 卡池 / 语言（上游列表页也是这么取的）
                using SqliteCommand lastCommand = connection.CreateCommand();
                lastCommand.CommandText = $"SELECT Name, GachaType, Lang FROM {table} WHERE Uid = @uid ORDER BY Id DESC LIMIT 1;";
                lastCommand.Parameters.AddWithValue("@uid", uid);
                using SqliteDataReader lastReader = lastCommand.ExecuteReader();
                if (!lastReader.Read())
                {
                    continue;
                }
                string name = lastReader.IsDBNull(0) ? "" : lastReader.GetString(0);
                int gachaType = lastReader.GetInt32(1);
                string? lang = lastReader.IsDBNull(2) ? null : lastReader.GetString(2);
                result.Add(new GachaArchiveSummary(game, uid, count, lang, lastTime, name, gachaType));
            }
        }
        return result;
    }


    /// <summary>某 UID 在某游戏下最近一次使用的语言（没有记录则为 <see langword="null"/>）。</summary>
    public string? GetLang(string game, long uid)
    {
        string table = TableName(game);
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT Lang FROM {table} WHERE Uid = @uid AND Lang IS NOT NULL ORDER BY Id DESC LIMIT 1;";
        command.Parameters.AddWithValue("@uid", uid);
        object? value = command.ExecuteScalar();
        return value is null or DBNull ? null : (string)value;
    }


    /// <summary>按 UID + 时间段删除（Starward <c>DeleteGachaLogByTime</c>）。</summary>
    public int DeleteRange(string game, long uid, DateTime begin, DateTime end)
    {
        string table = TableName(game);
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE Uid = @uid AND Time >= @begin AND Time <= @end;";
        command.Parameters.AddWithValue("@uid", uid);
        command.Parameters.AddWithValue("@begin", begin.ToString(TimeFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@end", end.ToString(TimeFormat, CultureInfo.InvariantCulture));
        return command.ExecuteNonQuery();
    }


    /// <summary>删除某 UID 的全部记录（Starward <c>DeleteUid</c>）。</summary>
    public int DeleteUid(string game, long uid)
    {
        string table = TableName(game);
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE Uid = @uid;";
        command.Parameters.AddWithValue("@uid", uid);
        return command.ExecuteNonQuery();
    }


    /// <summary>读取某游戏最近一次保存的抽卡链接（Starward <c>GachaLogUrl</c> 表）。</summary>
    public string? GetGachaLogUrl(string gameBiz)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Url FROM GachaLogUrl WHERE GameBiz = @gameBiz LIMIT 1;";
        command.Parameters.AddWithValue("@gameBiz", gameBiz);
        object? value = command.ExecuteScalar();
        return value is null or DBNull ? null : (string)value;
    }


    /// <summary>保存抽卡链接（Starward <c>GetUidFromGachaLogUrl</c> 里的 <c>INSERT OR REPLACE</c>）。</summary>
    public void SaveGachaLogUrl(string gameBiz, long uid, string url)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO GachaLogUrl (GameBiz, Uid, Url, Time) VALUES (@GameBiz, @Uid, @Url, @Time);
            """;
        command.Parameters.AddWithValue("@GameBiz", gameBiz);
        command.Parameters.AddWithValue("@Uid", uid);
        command.Parameters.AddWithValue("@Url", url);
        command.Parameters.AddWithValue("@Time", DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }


    private static IReadOnlyList<GachaRecord> ReadRecords(string game, SqliteCommand command)
    {
        using SqliteDataReader reader = command.ExecuteReader();
        var records = new List<GachaRecord>();
        while (reader.Read())
        {
            int gachaType = reader.GetInt32(reader.GetOrdinal("GachaType"));
            records.Add(new GachaRecord
            {
                Uid = reader.GetInt64(reader.GetOrdinal("Uid")),
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Time = ParseTime(reader.GetString(reader.GetOrdinal("Time"))),
                ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                ItemType = reader.GetString(reader.GetOrdinal("ItemType")),
                RankType = reader.GetInt32(reader.GetOrdinal("RankType")),
                GachaType = gachaType,
                GachaTypeName = GachaService.GetPoolName(game, gachaType),
                Count = reader.GetInt32(reader.GetOrdinal("Count")),
                GachaId = game == StarRail ? reader.GetInt32(reader.GetOrdinal("GachaId")) : 0,
                IsUp = false,
            });
        }
        return records;
    }


    private static DateTime ParseTime(string text)
        => DateTime.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime exact)
            ? exact
            : DateTime.Parse(text, CultureInfo.InvariantCulture);
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
