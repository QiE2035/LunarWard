using System.Text.Json;
using LunarWard.Launch.Database;

namespace LunarWard.Wine;

/// <summary>
/// 已保存游戏账号的 SQLite 存储（对应上游 <c>GameAccount</c> 表 +
/// <c>GameAccountService.SaveGameAccount/DeleteGameAccount</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 之前这里是自造的 <c>accounts.json</c>（明文 JSON，权限 0600）。按「本地记录存储一律与上游
/// 一致用 SQLite」改为表存储，列顺序与含义照抄上游
/// <c>DatabaseService.cs</c> 的 <c>GameAccount</c>：<c>SHA256 / GameBiz / Uid / Name / Value / Time</c>。
/// </para>
/// <para>
/// <b>安全</b>：<c>Value</c> 是账号凭据（注册表 ADL 二进制，内含 cookie 明文），与上游一样
/// 以 <c>BLOB</c> 原样保存，不做额外加密；库文件权限在打开时收紧到 0600（见
/// <see cref="OpenDefault" />），不写日志、不上传。
/// </para>
/// <para>
/// 上游 <c>GameBiz</c> 列历史上是枚举整数、v21 迁移后是区服字符串；这里直接用字符串
/// （<c>hk4e_cn</c> / <c>hkrpg_global</c> …），与 <see cref="GameAccountSnapshot.GameBiz" /> 一致。
/// </para>
/// </remarks>
public sealed class GameAccountStore
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly LunarWardDatabase _database;


    public GameAccountStore(LunarWardDatabase database)
    {
        _database = database;
        LimitPermissions();
    }


    /// <summary>
    /// 打开默认库（<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>），并把文件权限收紧到 0600。
    /// </summary>
    public static GameAccountStore OpenDefault(string dataHome)
        => new(new LunarWardDatabase(Path.Join(dataHome, LunarWardDatabase.FileName)));


    /// <summary>
    /// 把旧版（LunarWard 早期实现）的 <c>accounts.json</c> 导入 <c>GameAccount</c> 表。
    /// </summary>
    /// <remarks>
    /// 上游没有这个文件（它一直用数据库），这是 LunarWard 自己多出来的存储；升级时自动导入
    /// 并改名为 <c>accounts.json.migrated</c>（不直接删，便于用户回滚）。文件不存在或坏掉都
    /// 静默跳过 —— 启动路径不能因为一个旧文件报错。
    /// </remarks>
    /// <returns>是否发现并处理了旧文件。</returns>
    public bool MigrateLegacyJson(string dataHome)
    {
        string json = Path.Join(dataHome, "accounts.json");
        if (!File.Exists(json))
        {
            return false;
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (GameAccountSnapshot account in
                     JsonSerializer.Deserialize<List<GameAccountSnapshot>>(File.ReadAllText(json), options) ?? [])
            {
                if (!string.IsNullOrWhiteSpace(account.Sha256))
                {
                    Save(account);
                }
            }
        }
        catch (Exception)
        {
            // 旧文件损坏：不动它，也不影响启动；用户仍可手工排查。
            return false;
        }

        try
        {
            File.Move(json, json + ".migrated", overwrite: true);
        }
        catch (IOException)
        {
        }

        return true;
    }


    /// <summary>读全部已保存账号（按保存时间倒序），可按区服过滤。</summary>
    public List<GameAccountSnapshot> GetAll(string? gameBiz = null)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = gameBiz is null
            ? "SELECT SHA256, GameBiz, Uid, Name, Value, Time FROM GameAccount ORDER BY Time DESC;"
            : "SELECT SHA256, GameBiz, Uid, Name, Value, Time FROM GameAccount WHERE GameBiz=@biz ORDER BY Time DESC;";
        if (gameBiz is not null)
        {
            command.Parameters.AddWithValue("@biz", gameBiz);
        }

        var list = new List<GameAccountSnapshot>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(Read(reader));
        }

        return list;
    }


    /// <summary>按 SHA256 取一个账号（上游以内容哈希作主键）。</summary>
    public GameAccountSnapshot? Get(string sha256)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT SHA256, GameBiz, Uid, Name, Value, Time FROM GameAccount WHERE SHA256=@sha;";
        command.Parameters.AddWithValue("@sha", sha256);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }


    /// <summary>保存（同 SHA256 覆盖），对应上游 <c>INSERT OR REPLACE INTO GameAccount</c>。</summary>
    public void Save(GameAccountSnapshot account)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO GameAccount (SHA256, GameBiz, Uid, Name, Value, Time)
            VALUES (@sha, @biz, @uid, @name, @value, @time);
            """;
        command.Parameters.AddWithValue("@sha", account.Sha256);
        command.Parameters.AddWithValue("@biz", account.GameBiz);
        command.Parameters.AddWithValue("@uid", account.Uid);
        command.Parameters.AddWithValue("@name", account.Name ?? "");
        command.Parameters.AddWithValue("@value", account.Value);
        command.Parameters.AddWithValue("@time", account.Time.ToString(TimeFormat));
        command.ExecuteNonQuery();
    }


    /// <summary>重命名（只改 Name 列，对应上游账号重命名）。</summary>
    public void Rename(string sha256, string name)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE GameAccount SET Name=@name WHERE SHA256=@sha;";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@sha", sha256);
        command.ExecuteNonQuery();
    }


    /// <summary>删除，对应上游 <c>DELETE FROM GameAccount WHERE SHA256=@sha</c>。</summary>
    public void Delete(string sha256)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GameAccount WHERE SHA256=@sha;";
        command.Parameters.AddWithValue("@sha", sha256);
        command.ExecuteNonQuery();
    }


    /// <summary>
    /// 把库文件权限收紧到 0600（凭据文件，与上游「只在本机」口径一致）。
    /// 具体实现与侧车文件处理见 <see cref="LunarWardDatabase.LimitPermissions"/>。
    /// </summary>
    private void LimitPermissions() => _database.LimitPermissions();


    private static GameAccountSnapshot Read(Microsoft.Data.Sqlite.SqliteDataReader reader)
        => new()
        {
            Sha256 = reader.GetString(0),
            GameBiz = reader.GetString(1),
            Uid = reader.GetInt64(2),
            Name = reader.GetString(3),
            Value = (byte[])reader["Value"],
            Time = DateTime.TryParse(reader.GetString(5), out DateTime time) ? time : DateTime.Now,
        };
}
