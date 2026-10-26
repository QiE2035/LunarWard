using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;

namespace LunarWard.Launch.Database;

/// <summary>
/// 单个数据库文件的实例封装；建库与迁移全部交给 <see cref="DatabaseService"/>。
/// </summary>
/// <remarks>
/// <para>
/// 上游 Starward 的 <c>DatabaseService</c> 是静态单例：全局只有一个
/// <c>StarwardDatabase.db</c>，<c>SetDatabase(folder)</c> 决定路径。LunarWard 除了默认库
/// 还需要同时打开「抓包库」（<c>CaptureMode</c>），所以保留这个实例封装：
/// 它只是一个 <c>(路径 → 连接)</c> 的工厂，schema / 版本号 / 迁移 SQL 与上游
/// <see cref="DatabaseService.DatabaseSqls"/> 是同一份代码（见 DatabaseService.cs）。
/// </para>
/// <para>
/// Linux 差异（上游没有的两件事）：文件权限收紧到 0600、退出前 checkpoint WAL。
/// 这是为了不把 cookie / authkey 留在 0644 的 <c>-wal</c> / <c>-shm</c> 里。
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
        _connectionString = $"DataSource={Path};";

        // 库里有账号 cookie / 抽卡 authkey：建库前收紧目录，建库后收紧主库与侧车文件。
        LimitPermissions();
        DatabaseService.InitializeDatabase(_connectionString, Path);
        LimitPermissions();
    }


    public string Path { get; }


    public SqliteConnection CreateConnection()
    {
        SqliteConnection connection = DatabaseService.CreateConnection(_connectionString);

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
            using SqliteConnection connection = CreateConnection();
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
    /// 把整库的一致性快照写到 <paramref name="destination"/>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 做法与上游 <c>DatabaseService.BackupDatabase</c> 一致：先 <c>VACUUM</c> 整理源库，
    /// 再用 <see cref="SqliteConnection.BackupDatabase"/> 在线备份出单文件。
    /// </para>
    /// <para>
    /// 目标路径已存在时先写同目录的临时文件再原子 rename，避免覆盖到一半留下半个备份。
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

        try
        {
            using (SqliteConnection connection = CreateConnection())
            {
                connection.Execute("VACUUM;", commandType: CommandType.Text);
                using SqliteConnection backupCon = new($"DataSource={temp}; Pooling=False;");
                backupCon.Open();
                connection.BackupDatabase(backupCon);
            }

            File.Move(temp, full, overwrite: true);
            TrySetOwnerOnly(full);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
