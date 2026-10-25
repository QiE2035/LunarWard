namespace LunarWard.Core.SelfQuery;

/// <summary>
/// 账号记录（游戏内「数据查询」）的持久化接口。
/// </summary>
/// <remarks>
/// 上游 Starward 的 <c>SelfQueryService</c> 直接写 SQLite；这里把存储抽成接口，
/// 让 <see cref="SelfQueryService"/> 留在 Core（只依赖接口），SQLite 实现放在
/// <c>LunarWard.Launch.Database</c>（与抽卡表同一个库文件）。
/// </remarks>
public interface ISelfQueryStore
{
    long GetGenshinLastId(long uid, GenshinQueryType type);

    (long Add, long Sub) GetGenshinQueryItemsNumSum(long uid, GenshinQueryType type);

    void InsertGenshinQueryItems(long uid, GenshinQueryType type, List<GenshinQueryItem> items, bool all = false);

    long GetStarRailLastId(long uid, StarRailQueryType type);

    (long Add, long Sub) GetStarRailQueryItemsNumSum(long uid, StarRailQueryType type);

    void InsertStarRailQueryItems(long uid, StarRailQueryType type, List<StarRailQueryItem> items, bool all = false);

    long GetZZZLastId(long uid, ZZZQueryType type);

    (long Add, long Sub) GetZZZQueryItemsNumSum(long uid, ZZZQueryType type);

    void InsertZZZQueryItems(long uid, ZZZQueryType type, List<ZZZQueryItem> items, bool all = false);
}
