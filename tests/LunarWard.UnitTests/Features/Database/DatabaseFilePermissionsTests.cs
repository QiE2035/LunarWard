using LunarWard.Features.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LunarWard.UnitTests.Features.Database;

/// <summary>
/// 数据库文件权限收紧（<see cref="DatabaseFilePermissions"/>）：主库与 WAL 侧车文件都必须是 0600。
/// </summary>
/// <remarks>
/// <para>
/// 这是原 <c>DatabasePermissionTests</c>（fold-projects 时随旧数据层一起删掉）的语义补回：
/// 审计 RK-01 指出 WAL 模式下 <c>-wal</c> / <c>-shm</c> 不继承主库权限（按 umask 得 0644），
/// 账号 cookie 与抽卡记录会以明文落在侧车文件里。现在 <see cref="DatabaseService.CreateConnection"/>
/// 与初始化流程都会调用 <see cref="DatabaseFilePermissions.Harden"/>，这里锁住该行为。
/// </para>
/// <para>上游没有对应实现（Windows 依赖用户目录 ACL），属 Linux 安全补强，见 docs/09。</para>
/// <para>非 Linux 平台直接跳过：<see cref="File.GetUnixFileMode"/> 不适用。</para>
/// </remarks>
[Collection(GlobalStaticsCollection.Name)]
public class DatabaseFilePermissionsTests
{

    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode GroupReadableFile =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;



    [Fact]
    public void Harden_SetsOwnerOnlyOnDatabaseFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp = new TempDirectory();
        string path = temp.Combine("LunarWardDatabase.db");
        File.WriteAllText(path, "");
        File.SetUnixFileMode(path, GroupReadableFile);

        DatabaseFilePermissions.Harden(path);

        Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path));
    }



    [Fact]
    public void Harden_SetsOwnerOnlyOnWalAndShmSidecars()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp = new TempDirectory();
        string path = temp.Combine("LunarWardDatabase.db");
        foreach (string file in new[] { path, path + "-wal", path + "-shm" })
        {
            File.WriteAllText(file, "");
            File.SetUnixFileMode(file, GroupReadableFile);
        }

        DatabaseFilePermissions.Harden(path);

        Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path));
        Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path + "-wal"));
        Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path + "-shm"));
    }



    [Fact]
    public void Harden_IgnoresConnectionStringOptions()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp = new TempDirectory();
        string path = temp.Combine("LunarWardDatabase.db");
        File.WriteAllText(path, "");
        File.SetUnixFileMode(path, GroupReadableFile);

        // Microsoft.Data.Sqlite 的连接串里路径后面可能跟分号选项；只应处理路径部分。
        DatabaseFilePermissions.Harden($"{path};Pooling=False;Cache=Shared;");

        Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path));
    }



    [Fact]
    public void Harden_DoesNotThrowWhenFilesAreMissing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp = new TempDirectory();

        // 侧车文件由 SQLite 首次写入时才创建，缺失时应当静默跳过。
        Exception? exception = Record.Exception(() =>
            DatabaseFilePermissions.Harden(temp.Combine("not-created-yet.db")));

        Assert.Null(exception);
    }



    [Fact]
    public void CreateConnection_HardensDatabaseAndWalSidecars()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp = new TempDirectory();
        DatabaseService.SetDatabase(temp.Path);
        string path = Path.Join(temp.Path, "LunarWardDatabase.db");

        using (SqliteConnection connection = DatabaseService.CreateConnection())
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Secret (Value TEXT);
                INSERT INTO Secret VALUES ('cookie=secret');
                """;
            command.ExecuteNonQuery();

            Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path));
            // WAL 模式：写入后一定存在 -wal 侧车；-shm 允许由 SQLite 延迟创建。
            Assert.True(File.Exists(path + "-wal"), "WAL 模式下应有 -wal 侧车文件");
            Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path + "-wal"));
            if (File.Exists(path + "-shm"))
            {
                Assert.Equal(OwnerOnlyFile, File.GetUnixFileMode(path + "-shm"));
            }
        }
    }

}
