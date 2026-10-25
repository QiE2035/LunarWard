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
/// 目前已建：抽卡四表 + 三张 wiki 信息表 + 千星奇域两表 + 账号记录三表 + 游戏账号
/// <c>GameAccount</c> + 游戏时长 <c>PlayTimeItem/PlayTimeStats</c>；Starward 其余表
/// （活动记录、截图等）在 LunarWard 里仍是独立实现，尚未搬进来。
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

        // 库里有账号 cookie / 抽卡 authkey：建库前收紧目录，建库后收紧主库与侧车文件。
        LimitPermissions();
        Initialize();
        LimitPermissions();
    }


    public string Path { get; }


    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // WAL 模式下 SQLite 会在连接打开时新建 -wal / -shm；它们不继承主库权限，
        // 按 umask 得 0644，凭据明文会落在这里。每次开连接都收紧一次（幂等）。
        LimitSidecarPermissions();
        return connection;
    }


    /// <summary>
    /// 把主库文件收紧到 0600（凭据文件，与上游「只在本机」口径一致）。
    /// </summary>
    public void LimitPermissions() => TrySetOwnerOnly(Path);


    /// <summary>把 <c>-wal</c> / <c>-shm</c> 收紧到 0600；文件不存在时跳过。</summary>
    public void LimitSidecarPermissions()
    {
        TrySetOwnerOnly(Path + "-wal");
        TrySetOwnerOnly(Path + "-shm");
    }


    /// <summary>
    /// 退出前把 WAL 合并回主库（<c>TRUNCATE</c> 会连带删除 <c>-wal</c> / <c>-shm</c>）。
    /// </summary>
    /// <remarks>
    /// 不 checkpoint 的话，最近一次写入的明文凭据会留在 0644 的侧车文件里直到下次打开；
    /// 这一步让「退出后目录里只剩一个 0600 的库」。
    /// </remarks>
    public void Checkpoint()
    {
        try
        {
            using SqliteConnection connection = new(_connectionString);
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // 校验/收尾失败不影响退出；最坏情况是侧车文件留着，权限已在打开时收紧。
        }
    }


    /// <summary>
    /// 退出钩子：对默认库做一次 checkpoint（不存在则跳过，不创建空库）。
    /// </summary>
    public static void CheckpointDefault(string dataHome)
    {
        string path = System.IO.Path.Combine(dataHome, FileName);
        if (!File.Exists(path))
        {
            return;
        }

        new LunarWardDatabase(path).Checkpoint();
    }


    /// <summary>文件存在时收紧到 0600；不支持 chmod 的环境静默跳过。</summary>
    private static void TrySetOwnerOnly(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
            // 非 Unix 文件系统（或没有权限）时保持默认；数据本身仍只在本机。
        }
    }


    /// <summary>
    /// 把整库的一致性快照写到 <paramref name="destination"/>（<c>VACUUM INTO</c>）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>VACUUM INTO</c> 是 SQLite 官方的「热备份」方式：写入过程带事务、产出单文件，
    /// 并自动把 WAL 合并进去，不需要 <c>sqlite3</c> 命令行或先 checkpoint。
    /// </para>
    /// <para>
    /// 目标路径已存在时先写同目录的临时文件再原子 rename，避免覆盖到一半留下半个备份
    /// （<c>VACUUM INTO</c> 本身拒绝写已存在的文件）。
    /// </para>
    /// </remarks>
    public void BackupTo(string destination)
    {
        string full = System.IO.Path.GetFullPath(destination);
        string? dir = System.IO.Path.GetDirectoryName(full);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string temp = full + ".tmp";
        if (File.Exists(temp))
        {
            File.Delete(temp);
        }

        using (SqliteConnection connection = CreateConnection())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "VACUUM INTO $path";
            command.Parameters.AddWithValue("$path", temp);
            command.ExecuteNonQuery();
        }

        File.Move(temp, full, overwrite: true);
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

            -- 抽卡物品信息（Starward Sql_v1 / Sql_v9 的 GachaInfo 三表，列/索引逐字照抄）
            -- 存 wiki 接口给的 Name + Icon URL，GachaLogStore.UpsertGachaInfo 写入、GetIconMap 读
            CREATE TABLE IF NOT EXISTS GenshinGachaInfo
            (
                Id          INTEGER NOT NULL PRIMARY KEY,
                Name        TEXT,
                Icon        TEXT,
                Element     INTEGER NOT NULL,
                Level       INTEGER NOT NULL,
                CatId       INTEGER NOT NULL,
                WeaponCatId INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_GenshinGachaInfo_Name ON GenshinGachaInfo (Name);

            CREATE TABLE IF NOT EXISTS StarRailGachaInfo
            (
                ItemId         INTEGER NOT NULL PRIMARY KEY,
                ItemName       TEXT,
                IconUrl        TEXT,
                DamageType     INTEGER NOT NULL,
                Rarity         INTEGER NOT NULL,
                AvatarBaseType INTEGER NOT NULL,
                WikiUrl        TEXT,
                IsSystem       INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_StarRailGachaInfo_Name ON StarRailGachaInfo (ItemName);

            CREATE TABLE IF NOT EXISTS ZZZGachaInfo
            (
                Id          INTEGER NOT NULL PRIMARY KEY,
                Name        TEXT,
                Icon        TEXT,
                Rarity      INTEGER NOT NULL,
                ElementType INTEGER NOT NULL,
                Profession  INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ZZZGachaInfo_Name ON ZZZGachaInfo (Name);

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

            -- 游戏时长：照抄上游 DatabaseService.cs:339-352（Sql_v2）与 :1045-1055（v21）。
            -- PlayTimeItem 是上游的「进程采样」表，LunarWard 不写（见 PlayTimeStore 注释）；
            -- 建出来只为表结构与上游一致。
            CREATE TABLE IF NOT EXISTS PlayTimeItem
            (
                TimeStamp INTEGER PRIMARY KEY,
                GameBiz   TEXT    NOT NULL,
                Pid       INTEGER NOT NULL,
                State     INTEGER NOT NULL,
                CursorPos INTEGER NOT NULL,
                Message   TEXT
            );
            CREATE INDEX IF NOT EXISTS IX_PlayTimeItem_GameBiz ON PlayTimeItem (GameBiz);
            CREATE INDEX IF NOT EXISTS IX_PlayTimeItem_Pid ON PlayTimeItem (Pid);
            CREATE INDEX IF NOT EXISTS IX_PlayTimeItem_State ON PlayTimeItem (State);

            CREATE TABLE IF NOT EXISTS GameAccount
            (
                SHA256  TEXT    NOT NULL PRIMARY KEY,
                GameBiz TEXT    NOT NULL,
                Uid     INTEGER NOT NULL,
                Name    TEXT    NOT NULL,
                Value   BLOB    NOT NULL,
                Time    TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_GameAccount_GameBiz ON GameAccount (GameBiz);
            CREATE TABLE IF NOT EXISTS PlayTimeStats
            (
                Id           INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                GameBiz      TEXT    NOT NULL,
                Pid          INTEGER NOT NULL,
                StartTime    INTEGER NOT NULL,
                EndTime      INTEGER NOT NULL,
                Interruption INTEGER NOT NULL DEFAULT 0,
                Type         INTEGER NOT NULL DEFAULT 0
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_PlayTimeStats_GameBiz_StartTime_Pid ON PlayTimeStats (GameBiz, StartTime, Pid);
            """;
        command.ExecuteNonQuery();
    }
}
