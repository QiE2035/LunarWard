using Microsoft.Data.Sqlite;
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

    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";


    private readonly GachaDatabase _database;


    public GenshinBeyondGachaStore(GachaDatabase database) => _database = database;


    /// <summary>按数据目录打开默认库（<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>）。</summary>
    public static GenshinBeyondGachaStore OpenDefault(string dataHome)
        => new(new GachaDatabase(Path.Join(dataHome, GachaDatabase.FileName)));


    // ── 记录 ────────────────────────────────────────────────────────────


    public List<long> GetUids()
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        // 上游 GetUids：SELECT DISTINCT Uid FROM GenshinBeyondGachaItem
        command.CommandText = $"SELECT DISTINCT Uid FROM {GachaTableName} ORDER BY Uid;";
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<long>();
        while (reader.Read())
        {
            list.Add(reader.GetInt64(0));
        }
        return list;
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
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {GachaTableName} WHERE Uid = @Uid;";
        command.Parameters.AddWithValue("@Uid", uid);
        return Convert.ToInt32(command.ExecuteScalar());
    }


    public long GetLastId(long uid)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT Id FROM {GachaTableName} WHERE Uid = @Uid ORDER BY Id DESC LIMIT 1;";
        command.Parameters.AddWithValue("@Uid", uid);
        return command.ExecuteScalar() is long id ? id : 0;
    }


    /// <summary>写入记录（上游 <c>INSERT OR REPLACE</c>，同 <c>(Uid, Id)</c> 覆盖）。</summary>
    public void InsertItems(IEnumerable<GenshinBeyondGachaItemEx> items)
    {
        List<GenshinBeyondGachaItemEx> list = items.ToList();
        if (list.Count == 0)
        {
            return;
        }

        using SqliteConnection connection = _database.CreateConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT OR REPLACE INTO {GachaTableName}
                (Uid, Id, Region, OpGachaType, ScheduleId, ItemType, ItemId, ItemName, RankType, IsUp, Time)
            VALUES (@Uid, @Id, @Region, @OpGachaType, @ScheduleId, @ItemType, @ItemId, @ItemName, @RankType, @IsUp, @Time);
            """;
        SqliteParameter pUid = command.Parameters.Add("@Uid", SqliteType.Integer);
        SqliteParameter pId = command.Parameters.Add("@Id", SqliteType.Integer);
        SqliteParameter pRegion = command.Parameters.Add("@Region", SqliteType.Text);
        SqliteParameter pOpGachaType = command.Parameters.Add("@OpGachaType", SqliteType.Integer);
        SqliteParameter pScheduleId = command.Parameters.Add("@ScheduleId", SqliteType.Integer);
        SqliteParameter pItemType = command.Parameters.Add("@ItemType", SqliteType.Text);
        SqliteParameter pItemId = command.Parameters.Add("@ItemId", SqliteType.Integer);
        SqliteParameter pItemName = command.Parameters.Add("@ItemName", SqliteType.Text);
        SqliteParameter pRankType = command.Parameters.Add("@RankType", SqliteType.Integer);
        SqliteParameter pIsUp = command.Parameters.Add("@IsUp", SqliteType.Integer);
        SqliteParameter pTime = command.Parameters.Add("@Time", SqliteType.Text);
        foreach (GenshinBeyondGachaItemEx item in list)
        {
            pUid.Value = item.Uid;
            pId.Value = item.Id;
            pRegion.Value = item.Region ?? "";
            pOpGachaType.Value = item.OpGachaType;
            pScheduleId.Value = item.ScheduleId;
            pItemType.Value = item.ItemType ?? "";
            pItemId.Value = item.ItemId;
            pItemName.Value = item.ItemName ?? "";
            pRankType.Value = item.RankType;
            pIsUp.Value = item.IsUp;
            pTime.Value = item.Time.ToString(TimeFormat);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }


    public int DeleteUid(long uid)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        // 上游 DeleteGachaLogAsync：DELETE FROM 表 WHERE Uid = @uid
        command.CommandText = $"DELETE FROM {GachaTableName} WHERE Uid = @Uid;";
        command.Parameters.AddWithValue("@Uid", uid);
        return command.ExecuteNonQuery();
    }


    public int DeleteByTime(long uid, DateTime begin, DateTime end)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        // 上游：DELETE FROM 表 WHERE Uid = @uid AND Time >= @begin AND Time <= @end
        command.CommandText = $"DELETE FROM {GachaTableName} WHERE Uid = @Uid AND Time >= @Begin AND Time <= @End;";
        command.Parameters.AddWithValue("@Uid", uid);
        command.Parameters.AddWithValue("@Begin", begin.ToString(TimeFormat));
        command.Parameters.AddWithValue("@End", end.ToString(TimeFormat));
        return command.ExecuteNonQuery();
    }


    // ── 物品图标（GenshinBeyondGachaInfo） ──────────────────────────────


    public string? GetIcon(int itemId)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Icon FROM GenshinBeyondGachaInfo WHERE Id = @Id LIMIT 1;";
        command.Parameters.AddWithValue("@Id", itemId);
        return command.ExecuteScalar() as string;
    }


    /// <summary>整表替换物品图标（上游 <c>UpdateGachaInfoAsync</c> 的 <c>INSERT OR REPLACE</c>）。</summary>
    public void ReplaceInfo(IEnumerable<GenshinBeyondGachaInfo> info)
    {
        List<GenshinBeyondGachaInfo> list = info.ToList();
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO GenshinBeyondGachaInfo (Id, Name, Rank, Icon) VALUES (@Id, @Name, @Rank, @Icon);
            """;
        SqliteParameter pId = command.Parameters.Add("@Id", SqliteType.Integer);
        SqliteParameter pName = command.Parameters.Add("@Name", SqliteType.Text);
        SqliteParameter pRank = command.Parameters.Add("@Rank", SqliteType.Integer);
        SqliteParameter pIcon = command.Parameters.Add("@Icon", SqliteType.Text);
        foreach (GenshinBeyondGachaInfo item in list)
        {
            pId.Value = item.Id;
            pName.Value = item.Name ?? "";
            pRank.Value = item.Rank;
            pIcon.Value = item.Icon ?? "";
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }


    // ── 缓存 URL（GachaLogUrl，GameBiz = hk4eugc） ─────────────────────


    public string? GetUrl(long uid)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Url FROM GachaLogUrl WHERE Uid = @Uid AND GameBiz = @GameBiz LIMIT 1;";
        command.Parameters.AddWithValue("@Uid", uid);
        command.Parameters.AddWithValue("@GameBiz", GameBizUgc);
        return command.ExecuteScalar() as string;
    }


    public void SetUrl(long uid, string url)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        // 上游：INSERT OR REPLACE INTO GachaLogUrl (GameBiz, Uid, Url, Time)
        command.CommandText = """
            INSERT OR REPLACE INTO GachaLogUrl (GameBiz, Uid, Url, Time) VALUES (@GameBiz, @Uid, @Url, @Time);
            """;
        command.Parameters.AddWithValue("@GameBiz", GameBizUgc);
        command.Parameters.AddWithValue("@Uid", uid);
        command.Parameters.AddWithValue("@Url", url);
        command.Parameters.AddWithValue("@Time", DateTime.Now.ToString(TimeFormat));
        command.ExecuteNonQuery();
    }


    private List<GenshinBeyondGachaItemEx> Query(string sql, long uid)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Uid", uid);
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<GenshinBeyondGachaItemEx>();
        while (reader.Read())
        {
            list.Add(new GenshinBeyondGachaItemEx
            {
                Uid = reader.GetInt64(0),
                Id = reader.GetInt64(1),
                Region = reader.IsDBNull(2) ? "" : reader.GetString(2),
                OpGachaType = reader.GetInt32(3),
                ScheduleId = reader.GetInt32(4),
                ItemType = reader.IsDBNull(5) ? "" : reader.GetString(5),
                ItemId = reader.GetInt32(6),
                ItemName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                RankType = reader.GetInt32(8),
                IsUp = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                Time = reader.GetDateTime(10),
                Icon = reader.IsDBNull(11) ? "" : reader.GetString(11),
            });
        }
        return list;
    }

}
