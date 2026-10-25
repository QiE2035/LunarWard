using LunarWard.Core.SelfQuery;
using Microsoft.Data.Sqlite;

namespace LunarWard.Launch.Database;

/// <summary>
/// 账号记录（游戏内「数据查询」）的 SQLite 存储（Starward <c>SelfQueryService</c> 里的 SQL 部分）。
/// </summary>
/// <remarks>
/// <para>
/// 表结构、SQL 语义与上游一致：<c>GenshinQueryItem</c> / <c>StarRailQueryItem</c> /
/// <c>ZZZQueryItem</c>（DDL 见 <see cref="LunarWardDatabase"/>），
/// <c>INSERT OR REPLACE</c> 按主键覆盖、月份用 <c>DateTime LIKE 'yyyy-MM%'</c> 过滤、
/// 「检索全部」先按涉及月份 DELETE 再整体重写。
/// </para>
/// <para>
/// 与原 JSON 实现的差异只有持久化方式；方法签名保持一一对应，页面 VM 不用改逻辑。
/// </para>
/// </remarks>
public sealed class SelfQueryLogStore : ISelfQueryStore
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly LunarWardDatabase _database;


    public SelfQueryLogStore(LunarWardDatabase database) => _database = database;


    /// <summary>按数据目录打开默认库（<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>）。</summary>
    public static SelfQueryLogStore OpenDefault(string dataHome)
        => new(new LunarWardDatabase(Path.Join(dataHome, LunarWardDatabase.FileName)));


    #region Genshin


    public List<long> GetGenshinUids()
        => QueryLongs("SELECT DISTINCT Uid FROM GenshinQueryItem ORDER BY Uid;");


    public (long Add, long Sub) GetGenshinQueryItemsNumSum(long uid, GenshinQueryType type)
        => QueryNumSum("GenshinQueryItem", uid, (int)type);


    public long GetGenshinLastId(long uid, GenshinQueryType type)
        => QueryLastId("GenshinQueryItem", uid, (int)type);


    public List<string> GetGenshinMonths(long uid, GenshinQueryType type)
        => QueryMonths("GenshinQueryItem", "DateTime", uid, (int)type);


    public List<GenshinQueryItem> GetGenshinQueryItems(long uid, GenshinQueryType type, string? month)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = month is null
            ? "SELECT * FROM GenshinQueryItem WHERE Uid=@uid AND Type=@type ORDER BY DateTime DESC;"
            : "SELECT * FROM GenshinQueryItem WHERE Uid=@uid AND Type=@type AND DateTime LIKE @month ORDER BY DateTime DESC;";
        command.Parameters.AddWithValue("@uid", uid);
        command.Parameters.AddWithValue("@type", (int)type);
        if (month is not null)
        {
            command.Parameters.AddWithValue("@month", month + "%");
        }
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<GenshinQueryItem>();
        while (reader.Read())
        {
            DateTime time = ParseTime(reader, "DateTime");
            list.Add(new GenshinQueryItem
            {
                Uid = reader.GetInt64(0),
                Id = reader.GetInt64(1),
                AddNum = reader.GetInt64(2),
                Reason = reader.IsDBNull(3) ? "" : reader.GetString(3),
                DateTime = time,
                Time = time,
                Type = (GenshinQueryType)reader.GetInt32(5),
                Icon = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Level = reader.GetInt32(7),
                Quality = reader.GetInt32(8),
                Name = reader.IsDBNull(9) ? "" : reader.GetString(9),
            });
        }
        return list;
    }


    public void DeleteGenshinMonth(long uid, GenshinQueryType type, string month)
        => DeleteMonth("GenshinQueryItem", "DateTime", uid, (int)type, month);


    public void InsertGenshinQueryItems(long uid, GenshinQueryType type, List<GenshinQueryItem> items, bool all = false)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        if (all)
        {
            foreach (var month in items.Select(x => x.DateTime.ToString("yyyy-MM")).Distinct())
            {
                Execute(connection, transaction,
                    "DELETE FROM GenshinQueryItem WHERE Uid=@uid AND Type=@type AND DateTime LIKE @time;",
                    ("@uid", uid), ("@type", (int)type), ("@time", month + "%"));
            }
        }
        foreach (var item in items)
        {
            Execute(connection, transaction, """
                INSERT OR REPLACE INTO GenshinQueryItem (Uid, Id, AddNum, Reason, DateTime, Type, Icon, Level, Quality, Name)
                VALUES (@Uid, @Id, @AddNum, @Reason, @DateTime, @Type, @Icon, @Level, @Quality, @Name);
                """,
                ("@Uid", uid), ("@Id", item.Id), ("@AddNum", item.AddNum), ("@Reason", item.Reason ?? ""),
                ("@DateTime", item.DateTime.ToString(TimeFormat)), ("@Type", (int)type),
                ("@Icon", item.Icon ?? ""), ("@Level", item.Level), ("@Quality", item.Quality), ("@Name", item.Name ?? ""));
        }
        transaction.Commit();
    }

    #endregion


    #region Star Rail


    public List<long> GetStarRailUids()
        => QueryLongs("SELECT DISTINCT Uid FROM StarRailQueryItem ORDER BY Uid;");


    public (long Add, long Sub) GetStarRailQueryItemsNumSum(long uid, StarRailQueryType type)
        => QueryNumSum("StarRailQueryItem", uid, (int)type);


    public long GetStarRailLastId(long uid, StarRailQueryType type)
        => QueryLastId("StarRailQueryItem", uid, (int)type);


    public List<string> GetStarRailMonths(long uid, StarRailQueryType type)
        => QueryMonths("StarRailQueryItem", "Time", uid, (int)type);


    public List<StarRailQueryItem> GetStarRailQueryItems(long uid, StarRailQueryType type, string? month)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = month is null
            ? "SELECT * FROM StarRailQueryItem WHERE Uid=@uid AND Type=@type ORDER BY Time DESC;"
            : "SELECT * FROM StarRailQueryItem WHERE Uid=@uid AND Type=@type AND Time LIKE @month ORDER BY Time DESC;";
        command.Parameters.AddWithValue("@uid", uid);
        command.Parameters.AddWithValue("@type", (int)type);
        if (month is not null)
        {
            command.Parameters.AddWithValue("@month", month + "%");
        }
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<StarRailQueryItem>();
        while (reader.Read())
        {
            list.Add(new StarRailQueryItem
            {
                Id = reader.GetInt64(0),
                Uid = reader.GetInt64(1),
                Type = (StarRailQueryType)reader.GetInt32(2),
                Action = reader.IsDBNull(3) ? "" : reader.GetString(3),
                AddNum = reader.GetInt64(4),
                Time = ParseTime(reader, "Time"),
                RelicName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                RelicLevel = reader.GetInt32(7),
                RelicRarity = reader.GetInt32(8),
                EquipmentName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                EquipmentLevel = reader.GetInt32(10),
                EquipmentRarity = reader.GetInt32(11),
            });
        }
        return list;
    }


    public void DeleteStarRailMonth(long uid, StarRailQueryType type, string month)
        => DeleteMonth("StarRailQueryItem", "Time", uid, (int)type, month);


    public void InsertStarRailQueryItems(long uid, StarRailQueryType type, List<StarRailQueryItem> items, bool all = false)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        if (all)
        {
            foreach (var month in items.Select(x => x.Time.ToString("yyyy-MM")).Distinct())
            {
                Execute(connection, transaction,
                    "DELETE FROM StarRailQueryItem WHERE Uid=@uid AND Type=@type AND Time LIKE @time;",
                    ("@uid", uid), ("@type", (int)type), ("@time", month + "%"));
            }
        }
        foreach (var item in items)
        {
            Execute(connection, transaction, """
                INSERT OR REPLACE INTO StarRailQueryItem (Id, Uid, Type, Action, AddNum, Time, RelicName, RelicLevel, RelicRarity, EquipmentName, EquipmentLevel, EquipmentRarity)
                VALUES (@Id, @Uid, @Type, @Action, @AddNum, @Time, @RelicName, @RelicLevel, @RelicRarity, @EquipmentName, @EquipmentLevel, @EquipmentRarity);
                """,
                ("@Id", item.Id), ("@Uid", uid), ("@Type", (int)type), ("@Action", item.Action ?? ""),
                ("@AddNum", item.AddNum), ("@Time", item.Time.ToString(TimeFormat)),
                ("@RelicName", item.RelicName ?? ""), ("@RelicLevel", item.RelicLevel), ("@RelicRarity", item.RelicRarity),
                ("@EquipmentName", item.EquipmentName ?? ""), ("@EquipmentLevel", item.EquipmentLevel), ("@EquipmentRarity", item.EquipmentRarity));
        }
        transaction.Commit();
    }

    #endregion


    #region ZZZ


    public List<long> GetZZZUids()
        => QueryLongs("SELECT DISTINCT Uid FROM ZZZQueryItem ORDER BY Uid;");


    public (long Add, long Sub) GetZZZQueryItemsNumSum(long uid, ZZZQueryType type)
        => QueryNumSum("ZZZQueryItem", uid, (int)type);


    public long GetZZZLastId(long uid, ZZZQueryType type)
        => QueryLastId("ZZZQueryItem", uid, (int)type);


    public List<string> GetZZZMonths(long uid, ZZZQueryType type)
        => QueryMonths("ZZZQueryItem", "DateTime", uid, (int)type);


    public List<ZZZQueryItem> GetZZZQueryItems(long uid, ZZZQueryType type, string? month)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = month is null
            ? "SELECT * FROM ZZZQueryItem WHERE Uid=@uid AND Type=@type ORDER BY DateTime DESC;"
            : "SELECT * FROM ZZZQueryItem WHERE Uid=@uid AND Type=@type AND DateTime LIKE @month ORDER BY DateTime DESC;";
        command.Parameters.AddWithValue("@uid", uid);
        command.Parameters.AddWithValue("@type", (int)type);
        if (month is not null)
        {
            command.Parameters.AddWithValue("@month", month + "%");
        }
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<ZZZQueryItem>();
        while (reader.Read())
        {
            list.Add(new ZZZQueryItem
            {
                Id = reader.GetInt64(0),
                Uid = reader.GetInt64(1),
                Type = (ZZZQueryType)reader.GetInt32(2),
                Reason = reader.IsDBNull(3) ? "" : reader.GetString(3),
                AddNum = reader.GetInt64(4),
                DateTime = ParseTime(reader, "DateTime"),
                EquipName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                EquipRarity = reader.GetInt32(7),
                EquipLevel = reader.GetInt32(8),
                WeaponName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                WeaponRarity = reader.GetInt32(10),
                WeaponLevel = reader.GetInt32(11),
                ClientIp = reader.IsDBNull(12) ? "" : reader.GetString(12),
                ActionName = reader.IsDBNull(13) ? "" : reader.GetString(13),
                CardType = reader.GetInt32(14),
                ItemName = reader.IsDBNull(15) ? "" : reader.GetString(15),
            });
        }
        return list;
    }


    public void DeleteZZZMonth(long uid, ZZZQueryType type, string month)
        => DeleteMonth("ZZZQueryItem", "DateTime", uid, (int)type, month);


    public void InsertZZZQueryItems(long uid, ZZZQueryType type, List<ZZZQueryItem> items, bool all = false)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        if (all)
        {
            foreach (var month in items.Select(x => x.DateTime.ToString("yyyy-MM")).Distinct())
            {
                Execute(connection, transaction,
                    "DELETE FROM ZZZQueryItem WHERE Uid=@uid AND Type=@type AND DateTime LIKE @time;",
                    ("@uid", uid), ("@type", (int)type), ("@time", month + "%"));
            }
        }
        foreach (var item in items)
        {
            Execute(connection, transaction, """
                INSERT OR REPLACE INTO ZZZQueryItem (Id, Uid, Type, Reason, AddNum, DateTime, EquipName, EquipRarity, EquipLevel, WeaponName, WeaponRarity, WeaponLevel, ClientIp, ActionName, CardType, ItemName)
                VALUES (@Id, @Uid, @Type, @Reason, @AddNum, @DateTime, @EquipName, @EquipRarity, @EquipLevel, @WeaponName, @WeaponRarity, @WeaponLevel, @ClientIp, @ActionName, @CardType, @ItemName);
                """,
                ("@Id", item.Id), ("@Uid", uid), ("@Type", (int)type), ("@Reason", item.Reason ?? ""),
                ("@AddNum", item.AddNum), ("@DateTime", item.DateTime.ToString(TimeFormat)),
                ("@EquipName", item.EquipName ?? ""), ("@EquipRarity", item.EquipRarity), ("@EquipLevel", item.EquipLevel),
                ("@WeaponName", item.WeaponName ?? ""), ("@WeaponRarity", item.WeaponRarity), ("@WeaponLevel", item.WeaponLevel),
                ("@ClientIp", item.ClientIp ?? ""), ("@ActionName", item.ActionName ?? ""),
                ("@CardType", item.CardType), ("@ItemName", item.ItemName ?? ""));
        }
        transaction.Commit();
    }

    #endregion


    #region SQL helpers


    private List<long> QueryLongs(string sql)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<long>();
        while (reader.Read())
        {
            list.Add(reader.GetInt64(0));
        }
        return list;
    }


    private (long Add, long Sub) QueryNumSum(string table, long uid, int type)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT IFNULL(SUM(CASE WHEN AddNum > 0 THEN AddNum END), 0),
                   IFNULL(SUM(CASE WHEN AddNum < 0 THEN AddNum END), 0)
            FROM {table} WHERE Uid=@uid AND Type=@type;
            """;
        command.Parameters.AddWithValue("@uid", uid);
        command.Parameters.AddWithValue("@type", type);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt64(0), reader.GetInt64(1)) : (0, 0);
    }


    private long QueryLastId(string table, long uid, int type)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT Id FROM {table} WHERE Uid=@uid AND Type=@type ORDER BY Id DESC LIMIT 1;";
        command.Parameters.AddWithValue("@uid", uid);
        command.Parameters.AddWithValue("@type", type);
        object? value = command.ExecuteScalar();
        return value is long id ? id : 0;
    }


    private List<string> QueryMonths(string table, string column, long uid, int type)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT DISTINCT substr({column}, 1, 7) FROM {table}
            WHERE Uid=@uid AND Type=@type AND {column} IS NOT NULL ORDER BY 1 DESC;
            """;
        command.Parameters.AddWithValue("@uid", uid);
        command.Parameters.AddWithValue("@type", type);
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
            {
                list.Add(reader.GetString(0));
            }
        }
        return list;
    }


    private void DeleteMonth(string table, string column, long uid, int type, string month)
    {
        using SqliteConnection connection = _database.CreateConnection();
        Execute(connection, null,
            $"DELETE FROM {table} WHERE Uid=@uid AND Type=@type AND {column} LIKE @time;",
            ("@uid", uid), ("@type", type), ("@time", month + "%"));
    }


    private static DateTime ParseTime(SqliteDataReader reader, string column)
    {
        int index = reader.GetOrdinal(column);
        return reader.IsDBNull(index)
            ? default
            : DateTime.TryParse(reader.GetString(index), out DateTime value) ? value : default;
    }


    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        command.ExecuteNonQuery();
    }

    #endregion
}
