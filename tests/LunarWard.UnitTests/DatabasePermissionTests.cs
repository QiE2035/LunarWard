using LunarWard.Launch.Database;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 凭据文件的权限模型：目录 0700、主库 0600、WAL 侧车 0600、退出时 checkpoint。
/// </summary>
/// <remarks>
/// 审计 RK-01：WAL 模式下 <c>.db-wal</c> / <c>.db-shm</c> 不继承主库权限（按 umask 得
/// 0644），账号 cookie 与抽卡 authkey 的明文会落在侧车文件里；数据目录此前也只是
/// 0755。这三个断言分别锁住「目录」「侧车」「退出清理」。
/// </remarks>
public sealed class DatabasePermissionTests : IDisposable
{
    private readonly string _dir = Path.Join(
        Path.GetTempPath(),
        "lunarward-perm-" + Guid.NewGuid().ToString("N"));


    public DatabasePermissionTests()
    {
        Directory.CreateDirectory(_dir);
    }


    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }


    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode OwnerOnlyDirectory =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;


    [Fact]
    public void WalAndShm_AreOwnerOnly_WhileConnectionOpen()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string path = Path.Join(_dir, LunarWardDatabase.FileName);
        var database = new LunarWardDatabase(path);

        using (Microsoft.Data.Sqlite.SqliteConnection connection = database.CreateConnection())
        {
            using Microsoft.Data.Sqlite.SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Secret (Value TEXT);
                INSERT INTO Secret VALUES ('cookie=secret');
                """;
            command.ExecuteNonQuery();

            Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path));
            Assert.True(File.Exists(path + "-wal"), "WAL 模式应有 -wal 侧车文件");
            Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path + "-wal"));
            Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path + "-shm"));
        }
    }


    [Fact]
    public void Checkpoint_RemovesWalSidecar()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string path = Path.Join(_dir, LunarWardDatabase.FileName);
        var database = new LunarWardDatabase(path);

        using (Microsoft.Data.Sqlite.SqliteConnection connection = database.CreateConnection())
        using (Microsoft.Data.Sqlite.SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE IF NOT EXISTS Secret (Value TEXT); INSERT INTO Secret VALUES ('x');";
            command.ExecuteNonQuery();
        }

        database.Checkpoint();

        // TRUNCATE 后最后一次连接关闭时 SQLite 会删掉 -wal；退一步接受「存在但为空」。
        Assert.True(
            !File.Exists(path + "-wal") || new FileInfo(path + "-wal").Length == 0,
            "checkpoint 后不应留下有内容的 -wal");
    }


    [Fact]
    public void EnsureDirectories_AreOwnerOnly()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var paths = new XdgPaths(
            Path.Join(_dir, "cfg"),
            Path.Join(_dir, "data"),
            Path.Join(_dir, "cache"));

        paths.EnsureDirectories();

        foreach (string dir in new[]
                 {
                     paths.ConfigHome, paths.DataHome, paths.CacheHome,
                     paths.LogDirectory, paths.PrefixesDirectory,
                 })
        {
            Assert.Equal(OwnerOnlyDirectory, File.GetUnixFileMode(dir));
        }
    }
}
