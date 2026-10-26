using LunarWard.Core.Gacha;
using LunarWard.Core.Gacha.Genshin;
using Xunit;

namespace LunarWard.UnitTests.Features.Gacha;

/// <summary>
/// 分页推进回归：上游 <c>GachaLogClient.GetGachaLogByTypeAsync</c> 固定每页 20 条，
/// 只有「本页满 20 条 且 本页最后一条 Id 大于上次的 end_id」时才把 page+1、end_id 推进到该 Id；
/// 否则立刻停止。这里用派生类替换 <c>GetGachaLogByQueryAsync</c>，不触网、不等待，验证推进规则。
/// </summary>
public class GachaPaginationTests
{
    private const string GachaUrl = "https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog?authkey=abc&lang=zh-cn";

    private sealed class PagedGenshinClient : GenshinGachaClient
    {
        private readonly Func<GachaLogQuery, List<GenshinGachaItem>> _page;

        public List<(int Page, long EndId, int Size)> Requests { get; } = new();

        public PagedGenshinClient(Func<GachaLogQuery, List<GenshinGachaItem>> page) => _page = page;

        protected override Task<List<T>> GetGachaLogByQueryAsync<T>(string gachaUrlPrefix, GachaLogQuery param, CancellationToken cancellationToken = default)
        {
            Requests.Add((param.Page, param.EndId, param.Size));
            return Task.FromResult(_page(param).Cast<T>().ToList());
        }
    }

    private static List<GenshinGachaItem> Items(long firstId, int count) => Enumerable.Range(0, count)
        .Select(i => new GenshinGachaItem
        {
            Id = firstId + i,
            Uid = 100000000,
            GachaType = 301,
            Name = "Test",
            ItemType = "武器",
            RankType = 3,
            Count = 1,
            Time = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
        })
        .ToList();

    private static IGachaType Type301 => new GenshinGachaType(301);


    [Fact]
    public async Task Pagination_AdvancesPageAndEndId_UntilShortPage()
    {
        var client = new PagedGenshinClient(param => param.Page switch
        {
            1 => Items(1, 20),
            2 => Items(21, 20),
            3 => Items(41, 20),
            _ => Items(61, 5),
        });

        var result = (await client.GetGachaLogAsync(GachaUrl, Type301)).ToList();

        Assert.Equal(65, result.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, client.Requests.Select(x => x.Page));
        Assert.Equal(new long[] { 0, 20, 40, 60 }, client.Requests.Select(x => x.EndId));
        Assert.All(client.Requests, x => Assert.Equal(20, x.Size));
    }


    [Fact]
    public async Task Pagination_StopsWhenLastIdDoesNotExceedEndId()
    {
        // 起始 end_id=999 时，第一页的最后一条 Id 不大于 999，必须只请求一次就停
        var client = new PagedGenshinClient(_ => Items(1, 20));

        var result = (await client.GetGachaLogAsync(GachaUrl, Type301, endId: 999)).ToList();

        Assert.Equal(20, result.Count);
        Assert.Single(client.Requests);
        Assert.Equal(0, client.Requests[0].EndId);
        Assert.Equal(1, client.Requests[0].Page);
    }


    [Fact]
    public async Task Pagination_StopsImmediately_OnEmptyFirstPage()
    {
        var client = new PagedGenshinClient(_ => new List<GenshinGachaItem>());

        var result = (await client.GetGachaLogAsync(GachaUrl, Type301)).ToList();

        Assert.Empty(result);
        Assert.Single(client.Requests);
    }


    [Fact]
    public async Task Pagination_DoesNotRequestASecondPage_ForPartialFirstPage()
    {
        var client = new PagedGenshinClient(_ => Items(1, 19));

        var result = (await client.GetGachaLogAsync(GachaUrl, Type301)).ToList();

        Assert.Equal(19, result.Count);
        Assert.Single(client.Requests);
    }
}
