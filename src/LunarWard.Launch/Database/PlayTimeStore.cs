using Microsoft.Data.Sqlite;

namespace LunarWard.Launch.Database;

/// <summary>
/// 一条已结算的游戏时长记录（Starward <c>PlayTimeStats</c> 表的一行）。
/// </summary>
/// <param name="GameBiz">游戏区服，如 <c>hk4e_cn</c>；对应上游 <c>PlayTimeStats.GameBiz</c>。</param>
/// <param name="Pid">游戏进程号；上游用于区分同一区服的多开。</param>
/// <param name="StartedAt">开始时间（Unix 毫秒，对应 <c>StartTime</c>）。</param>
/// <param name="EndedAt">结束时间（Unix 毫秒，对应 <c>EndTime</c>）。</param>
public sealed record PlayTimeRecord(string GameBiz, int Pid, long StartedAt, long EndedAt);


/// <summary>
/// 游戏时长统计的 SQLite 存储，表结构照抄 Starward <c>DatabaseService.cs</c> 的
/// <c>PlayTimeStats</c>（v21 DDL，含唯一索引 <c>IX_PlayTimeStats_GameBiz_StartTime_Pid</c>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>与上游的差异</b>：上游由一个后台线程每 ~1 秒采样一次进程状态写 <c>PlayTimeItem</c>
/// （<c>Features/PlayTime/PlayTimeRecordService.LogPlayTimeAsync</c>），退出时把这些采样
/// 结算成一行 <c>PlayTimeStats</c> 并删除采样。LunarWard 的相位引擎本来就知道进程的
/// 精确起止时间（<c>docs/05-ROADMAP.md</c> §3.3），所以直接写结算行、不写采样表 ——
/// 存储终点（<c>PlayTimeStats</c>）与统计口径一致，少的只是「进程被强杀时丢采样」那种场景。
/// </para>
/// <para>
/// 文件名与抽卡库相同：<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>，同一连接上建表，不另开文件。
/// </para>
/// </remarks>
public sealed class PlayTimeStore
{
    private readonly LunarWardDatabase _database;


    public PlayTimeStore(LunarWardDatabase database)
    {
        _database = database;
    }


    /// <summary>打开默认位置（<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>）的库。</summary>
    public static PlayTimeStore OpenDefault(string dataHome)
        => new(new LunarWardDatabase(Path.Join(dataHome, LunarWardDatabase.FileName)));


    /// <summary>
    /// 写入一条结算记录（对应上游 <c>INSERT OR REPLACE INTO PlayTimeStats</c>；
    /// 唯一索引同样是 <c>(GameBiz, StartTime, Pid)</c>，重复结算不会产生两行）。
    /// </summary>
    public void Save(string gameBiz, int pid, long startTimeMs, long endTimeMs)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO PlayTimeStats (GameBiz, Pid, StartTime, EndTime, Interruption, Type)
            VALUES (@gameBiz, @pid, @startTime, @endTime, 0, 0);
            """;
        command.Parameters.AddWithValue("@gameBiz", gameBiz);
        command.Parameters.AddWithValue("@pid", pid);
        command.Parameters.AddWithValue("@startTime", startTimeMs);
        command.Parameters.AddWithValue("@endTime", endTimeMs);
        command.ExecuteNonQuery();
    }


    /// <summary>全部记录，按开始时间倒序（上游 <c>ORDER BY TimeStamp</c> 的倒序版本）。</summary>
    public IReadOnlyList<PlayTimeRecord> GetAll(string? gameBiz = null)
    {
        using SqliteConnection connection = _database.CreateConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = gameBiz is null
            ? "SELECT GameBiz, Pid, StartTime, EndTime FROM PlayTimeStats ORDER BY StartTime DESC;"
            : "SELECT GameBiz, Pid, StartTime, EndTime FROM PlayTimeStats WHERE GameBiz = @gameBiz ORDER BY StartTime DESC;";
        if (gameBiz is not null)
        {
            command.Parameters.AddWithValue("@gameBiz", gameBiz);
        }

        var list = new List<PlayTimeRecord>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PlayTimeRecord(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt64(2),
                reader.GetInt64(3)));
        }

        return list;
    }
}
