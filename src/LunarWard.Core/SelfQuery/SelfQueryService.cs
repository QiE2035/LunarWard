using LunarWard.Core.HoYoPlay;

namespace LunarWard.Core.SelfQuery;

/// <summary>
/// 游戏内「数据查询」记录的拉取与统计。
///
/// <para>上游 <c>Starward.Features.SelfQuery.SelfQueryService</c> 直接走 SQLite；这里把
/// 持久化通过 <see cref="ISelfQueryStore"/> 注入（SQLite 实现见 <c>SelfQueryLogStore</c>），分页/去重/月份重写的语义保持一致。</para>
/// </summary>
public sealed class SelfQueryService
{

    private readonly ISelfQueryStore _store;

    private readonly SelfQueryClient _client;



    public SelfQueryService(ISelfQueryStore store, SelfQueryClient? client = null)
    {
        _store = store;
        _client = client ?? new SelfQueryClient();
    }



    public SelfQueryUserInfo? UserInfo => _client.UserInfo;



    public Task<SelfQueryUserInfo> InitializeAsync(string url, GameBiz gameBiz, CancellationToken cancellationToken = default)
    {
        return _client.InitializeAsync(url, gameBiz, cancellationToken);
    }


    public void EnsureInitialized() => _client.EnsureInitialized();


    public void Reset() => _client.Reset();





    #region Genshin

    public async Task<(long Add, long Sub)> UpdateGenshinQueryItemsAsync(GenshinQueryType type, IProgress<int>? progress = null, bool all = false, CancellationToken cancellationToken = default)
    {
        _client.EnsureInitialized();
        long uid = _client.UserInfo?.Uid ?? 0;
        long lastId = _store.GetGenshinLastId(uid, type);
        long endId = 0;
        var list = new List<GenshinQueryItem>();
        for (int i = 1; ; i++)
        {
            progress?.Report(i);
            var temp = await _client.GetGenshinQueryItemsAsync(type, endId, cancellationToken).ConfigureAwait(false);
            list.AddRange(temp);
            if (temp.Count < 20)
            {
                break;
            }
            endId = temp[^1].Id;
            if (!all && endId <= lastId)
            {
                break;
            }
        }
        _store.InsertGenshinQueryItems(uid, type, list, all);
        return _store.GetGenshinQueryItemsNumSum(uid, type);
    }

    #endregion





    #region Star Rail

    public async Task<(long Add, long Sub)> UpdateStarRailQueryItemsAsync(StarRailQueryType type, IProgress<int>? progress = null, bool all = false, CancellationToken cancellationToken = default)
    {
        _client.EnsureInitialized();
        long uid = _client.UserInfo?.Uid ?? 0;
        long lastId = _store.GetStarRailLastId(uid, type);
        long endId = 0;
        var list = new List<StarRailQueryItem>();
        for (int i = 1; ; i++)
        {
            progress?.Report(i);
            var temp = await _client.GetStarRailQueryItemsAsync(type, endId, 20, null, null, cancellationToken).ConfigureAwait(false);
            list.AddRange(temp);
            if (temp.Count < 20)
            {
                break;
            }
            endId = temp[^1].Id;
            if (!all && endId <= lastId)
            {
                break;
            }
        }
        _store.InsertStarRailQueryItems(uid, type, list, all);
        return _store.GetStarRailQueryItemsNumSum(uid, type);
    }

    #endregion





    #region ZZZ

    public async Task<(long Add, long Sub)> UpdateZZZQueryItemsAsync(ZZZQueryType type, IProgress<int>? progress = null, bool all = false, CancellationToken cancellationToken = default)
    {
        _client.EnsureInitialized();
        long uid = _client.UserInfo?.Uid ?? 0;
        long lastId = _store.GetZZZLastId(uid, type);
        long endId = 0;
        var list = new List<ZZZQueryItem>();
        for (int i = 1; ; i++)
        {
            progress?.Report(i);
            var temp = await _client.GetZZZQueryItemsAsync(type, endId, 20, null, null, cancellationToken).ConfigureAwait(false);
            list.AddRange(temp);
            if (temp.Count < 20)
            {
                break;
            }
            endId = temp[^1].Id;
            if (!all && endId <= lastId)
            {
                break;
            }
        }
        _store.InsertZZZQueryItems(uid, type, list, all);
        return _store.GetZZZQueryItemsNumSum(uid, type);
    }

    #endregion

}
