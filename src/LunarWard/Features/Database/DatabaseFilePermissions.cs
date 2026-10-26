using System.Runtime.InteropServices;

namespace LunarWard.Features.Database;

/// <summary>
/// 平台扩展：收紧数据库文件的权限。
/// </summary>
/// <remarks>
/// <para>
/// 上游 <c>Starward</c> 在 Windows 下依赖用户数据目录的 ACL（只有当前用户可读），
/// Linux 没有等价 ACL，因此在打开数据库后显式把主库与 WAL 侧车文件设成 <c>0600</c>。
/// </para>
/// <para>
/// 侧车文件（<c>-wal</c> / <c>-shm</c>）由 SQLite 在首次写入时才创建，所以这里只对“当前已存在”的
/// 文件生效；每次 <see cref="DatabaseService.CreateConnection"/> 打开连接时都会再收紧一次，
/// 正常使用后侧车文件会在下一次打开时被覆盖到，与旧 <c>LunarWardDatabase</c> 的做法一致。
/// </para>
/// <para>
/// 平台替换点：这是上游没有的 Linux 加固，属安全补强，不改变任何数据语义。
/// </para>
/// </remarks>
internal static class DatabaseFilePermissions
{

    private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;


    /// <summary>
    /// 把 <paramref name="dataSource"/> 指向的数据库文件及其 WAL 侧车文件权限收紧到 0600（仅 Linux）。
    /// </summary>
    public static void Harden(string? dataSource)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || string.IsNullOrWhiteSpace(dataSource))
        {
            return;
        }

        string path = StripConnectionOptions(dataSource);
        if (path.Length == 0)
        {
            return;
        }

        HardenFile(path);
        HardenFile(path + "-wal");
        HardenFile(path + "-shm");
    }


    private static void HardenFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetUnixFileMode(path, OwnerReadWrite);
            }
        }
        catch (Exception)
        {
            // 权限收紧失败不阻断启动：文件可能被其他用户挂载点限制，或文件系统不支持 chmod。
        }
    }


    /// <summary>
    /// <see cref="Microsoft.Data.Sqlite.SqliteConnection.DataSource"/> 里可能带 <c>Mode=...</c> 之类的分号选项，
    /// 这里只取文件路径部分。
    /// </summary>
    private static string StripConnectionOptions(string dataSource)
    {
        int index = dataSource.IndexOf(';');
        string path = index >= 0 ? dataSource[..index] : dataSource;
        return path.Trim();
    }

}
