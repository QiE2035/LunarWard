using Microsoft.Data.Sqlite;

namespace LunarWard.Launch.Database;

/// <summary>
/// 本地数据库（Starward <c>Features/Database/DatabaseService</c> 的等价物，逐表补齐）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 把抽卡记录存在 <c>StarwardDatabase.db</c>（SQLite）里，三张表
/// <c>GenshinGachaItem</c> / <c>StarRailGachaItem</c> / <c>ZZZGachaItem</c>，
/// 外加 <c>GachaLogUrl</c>（每个账号最近一次用的抽卡链接）。
/// 这里按同一份 schema 建库（列名、主键、索引都照抄 <c>DatabaseService.Sql_v1</c> /
/// <c>Sql_v9</c> / <c>Sql_v12</c>），只是文件叫 <c>LunarWardDatabase.db</c>。
/// </para>
/// <para>
/// 只建抽卡相关的表：Starward 的其它表（游戏账号、活动记录、截图等）在 LunarWard
/// 里各自有独立的 JSON / 文件实现，不在本类范围内。
/// </para>
/// </remarks>
public sealed class LunarWardDatabase
{
    /// <summary>数据库文件名（Starward 是 <c>StarwardDatabase.db</c>）。</summary>
    public const string FileName = "LunarWardDatabase.db";


    private readonly string _connectionString;


    /// <param name="path">数据库文件路径（父目录自动创建）。</param>
    public LunarWardDatabase(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        string? dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _connectionString = $"Data Source={Path}";
        Initialize();
    }


    public string Path { get; }


    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }


    /// <summary>
    /// 建表（幂等）。DDL 与 Starward 的 <c>DatabaseService.Sql_v1</c>（抽卡表）、
    /// <c>Sql_v7</c>（账号记录 <c>GenshinQueryItem</c> / <c>StarRailQueryItem</c>）、
    /// <c>Sql_v9</c>（ZZZ 表 + <c>ZZZQueryItem</c>）、<c>Sql_v12</c>（GachaLogUrl）、<c>Sql_v16</c>/<c>Sql_v17</c>
    /// （千星奇域颂愿的 <c>GenshinBeyondGachaItem</c> / <c>GenshinBeyondGachaInfo</c>）逐字一致。
    /// </summary>
    private void Initialize()
    {
        using SqliteConnection connection = CreateConnection();
        using SqliteCommand command = connection.CreateCommand();

        // WAL 与 Starward 一致（Sql_v1 里只有 USER_VERSION==0 时设置，这里每次都设，幂等）
        command.CommandText = """
            PRAGMA JOURNAL_MODE = WAL;

            CREATE TABLE IF NOT EXISTS GachaLogUrl
            (
                GameBiz TEXT    NOT NULL,
                Uid     INTEGER NOT NULL,
                Url     TEXT    NOT NULL,
                Time    TEXT    NOT NULL,
                PRIMARY KEY (GameBiz, Uid)
            );

            CREATE TABLE IF NOT EXISTS GenshinGachaItem
            (
                Uid       INTEGER NOT NULL,
                Id        INTEGER NOT NULL,
                Name      TEXT    NOT NULL,
                Time      TEXT    NOT NULL,
                ItemId    INTEGER NOT NULL,
                ItemType  TEXT    NOT NULL,
                RankType  INTEGER NOT NULL,
                GachaType INTEGER NOT NULL,
                Count     INTEGER NOT NULL,
                Lang      TEXT,
                PRIMARY KEY (Uid, Id)
            );
            CREATE INDEX IF NOT EXISTS IX_GenshinGachaItem_Id ON GenshinGachaItem (Id);
            CREATE INDEX IF NOT EXISTS IX_GenshinGachaItem_RankType ON GenshinGachaItem (RankType);
            CREATE INDEX IF NOT EXISTS IX_GenshinGachaItem_GachaType ON GenshinGachaItem (GachaType);

            CREATE TABLE IF NOT EXISTS StarRailGachaItem
            (
                Uid       INTEGER NOT NULL,
                Id        INTEGER NOT NULL,
                Name      TEXT    NOT NULL,
                Time      TEXT    NOT NULL,
                ItemId    INTEGER NOT NULL,
                ItemType  TEXT    NOT NULL,
                RankType  INTEGER NOT NULL,
                GachaType INTEGER NOT NULL,
                GachaId   INTEGER NOT NULL,
                Count     INTEGER NOT NULL,
                Lang      TEXT,
                PRIMARY KEY (Uid, Id)
            );
            CREATE INDEX IF NOT EXISTS IX_StarRailGachaItem_Id ON StarRailGachaItem (Id);
            CREATE INDEX IF NOT EXISTS IX_StarRailGachaItem_RankType ON StarRailGachaItem (RankType);
            CREATE INDEX IF NOT EXISTS IX_StarRailGachaItem_GachaType ON StarRailGachaItem (GachaType);

            CREATE TABLE IF NOT EXISTS ZZZGachaItem
            (
                Uid       INTEGER NOT NULL,
                Id        INTEGER NOT NULL,
                Name      TEXT    NOT NULL,
                Time      TEXT    NOT NULL,
                ItemId    INTEGER NOT NULL,
                ItemType  TEXT    NOT NULL,
                RankType  INTEGER NOT NULL,
                GachaType INTEGER NOT NULL,
                Count     INTEGER NOT NULL,
                Lang      TEXT,
                PRIMARY KEY (Uid, Id)
            );
            CREATE INDEX IF NOT EXISTS IX_ZZZGachaItem_Id ON ZZZGachaItem (Id);
            CREATE INDEX IF NOT EXISTS IX_ZZZGachaItem_RankType ON ZZZGachaItem (RankType);
            CREATE INDEX IF NOT EXISTS IX_ZZZGachaItem_GachaType ON ZZZGachaItem (GachaType);

            -- 千星奇域颂愿（Starward Sql_v16/v17）：记录表 + 物品图标表
            CREATE TABLE IF NOT EXISTS GenshinBeyondGachaItem
            (
                Uid         INTEGER,
                Id          INTEGER,
                Region      TEXT,
                OpGachaType INTEGER,
                ScheduleId  INTEGER,
                ItemType    TEXT,
                ItemId      INTEGER,
                ItemName    TEXT,
                RankType    INTEGER,
                IsUp        INTEGER,
                Time        TEXT,
                PRIMARY KEY (Uid, Id)
            );
            CREATE INDEX IF NOT EXISTS IX_GenshinBeyondGachaItem_Id ON GenshinBeyondGachaItem (Id);
            CREATE INDEX IF NOT EXISTS IX_GenshinBeyondGachaItem_OpGachaType ON GenshinBeyondGachaItem (OpGachaType);

            CREATE TABLE IF NOT EXISTS GenshinBeyondGachaInfo
            (
                Id   INTEGER PRIMARY KEY,
                Name TEXT,
                Rank INTEGER NOT NULL,
                Icon TEXT
            );
            CREATE INDEX IF NOT EXISTS IX_GenshinBeyondGachaInfo_Name ON GenshinBeyondGachaInfo (Name);

            -- 账号记录（Starward Sql_v7 / Sql_v9 的 QueryItem 三表，列/索引逐字照抄）
            CREATE TABLE IF NOT EXISTS GenshinQueryItem
            (
                Uid      INTEGER NOT NULL,
                Id       INTEGER NOT NULL,
                AddNum   INTEGER NOT NULL,
                Reason   TEXT,
                DateTime TEXT,
                Type     INTEGER NOT NULL,
                Icon     TEXT,
                Level    INTEGER NOT NULL,
                Quality  INTEGER NOT NULL,
                Name     TEXT,
                PRIMARY KEY (Uid, Id)
            );
            CREATE INDEX IF NOT EXISTS IX_GenshinQueryItem_Id ON GenshinQueryItem (Id);
            CREATE INDEX IF NOT EXISTS IX_GenshinQueryItem_AddNum ON GenshinQueryItem (AddNum);
            CREATE INDEX IF NOT EXISTS IX_GenshinQueryItem_Reason ON GenshinQueryItem (Reason);
            CREATE INDEX IF NOT EXISTS IX_GenshinQueryItem_DateTime ON GenshinQueryItem (DateTime);
            CREATE INDEX IF NOT EXISTS IX_GenshinQueryItem_Type ON GenshinQueryItem (Type);

            CREATE TABLE IF NOT EXISTS StarRailQueryItem
            (
                Id              INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Uid             INTEGER NOT NULL,
                Type            INTEGER NOT NULL,
                Action          TEXT,
                AddNum          INTEGER NOT NULL,
                Time            TEXT,
                RelicName       TEXT,
                RelicLevel      INTEGER NOT NULL,
                RelicRarity     INTEGER NOT NULL,
                EquipmentName   TEXT,
                EquipmentLevel  INTEGER NOT NULL,
                EquipmentRarity INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_StarRailQueryItem_Uid ON StarRailQueryItem (Uid);
            CREATE INDEX IF NOT EXISTS IX_StarRailQueryItem_Type ON StarRailQueryItem (Type);
            CREATE INDEX IF NOT EXISTS IX_StarRailQueryItem_Action ON StarRailQueryItem (Action);
            CREATE INDEX IF NOT EXISTS IX_StarRailQueryItem_AddNum ON StarRailQueryItem (AddNum);
            CREATE INDEX IF NOT EXISTS IX_StarRailQueryItem_Time ON StarRailQueryItem (Time);

            CREATE TABLE IF NOT EXISTS ZZZQueryItem
            (
                Id           INTEGER NOT NULL,
                Uid          INTEGER NOT NULL,
                Type         INTEGER NOT NULL,
                Reason       TEXT,
                AddNum       INTEGER NOT NULL,
                DateTime     TEXT,
                EquipName    TEXT,
                EquipRarity  INTEGER NOT NULL,
                EquipLevel   INTEGER NOT NULL,
                WeaponName   TEXT,
                WeaponRarity INTEGER NOT NULL,
                WeaponLevel  INTEGER NOT NULL,
                ClientIp     TEXT,
                ActionName   TEXT,
                CardType     INTEGER NOT NULL,
                ItemName     TEXT,
                PRIMARY KEY (Uid, Id)
            );
            CREATE INDEX IF NOT EXISTS IX_ZZZQueryItem_Id ON ZZZQueryItem (Id);
            CREATE INDEX IF NOT EXISTS IX_ZZZQueryItem_Type ON ZZZQueryItem (Type);
            CREATE INDEX IF NOT EXISTS IX_ZZZQueryItem_Reason ON ZZZQueryItem (Reason);
            CREATE INDEX IF NOT EXISTS IX_ZZZQueryItem_AddNum ON ZZZQueryItem (AddNum);
            CREATE INDEX IF NOT EXISTS IX_ZZZQueryItem_DateTime ON ZZZQueryItem (DateTime);
            """;
        command.ExecuteNonQuery();
    }
}
