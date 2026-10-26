using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using LunarWard.App.Services;
using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.HoYoPlay;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 启动器首页资讯的「按游戏隔离」：<see cref="StarwardAssets"/> 不再共享一个可变的
/// <c>Content</c> 字段，缓存键带 gameId，晚到的旧游戏结果不会串到当前游戏。
/// </summary>
public sealed class StarwardAssetsContentTests
{
    private const string GenshinBiz = "hk4e_cn";
    private const string GenshinGameId = "1Z8W5NHUQb";
    private const string ZzzBiz = "nap_cn";
    private const string ZzzGameId = "T2S0Gz4Dr2";
    private const string StarRailBiz = "hkrpg_cn";


    [Fact]
    public async Task GetContentAsyncReturnsEachGamesOwnContent()
    {
        using var fixture = new AssetsFixture();

        var genshin = await fixture.Assets.GetContentAsync(LauncherId.ChinaOfficial, GenshinBiz);
        var zzz = await fixture.Assets.GetContentAsync(LauncherId.ChinaOfficial, ZzzBiz);
        // 30 分钟内的重复请求走缓存早返回（旧实现的 bug 就在这里返回别的游戏的数据）。
        var genshinAgain = await fixture.Assets.GetContentAsync(LauncherId.ChinaOfficial, GenshinBiz);

        Assert.Equal(GenshinGameId, genshin!.GameId.Id);
        Assert.Equal(GenshinGameId, genshinAgain!.GameId.Id);
        Assert.Equal(ZzzGameId, zzz!.GameId.Id);
        Assert.Contains(genshin.Posts, x => x.Title == "原神公告");
        Assert.Contains(zzz.Posts, x => x.Title == "绝区零公告");
        Assert.DoesNotContain(zzz.Posts, x => x.Title == "原神公告");
        Assert.Equal(2, fixture.Handler.ContentCalls);
    }


    [Fact]
    public async Task LateContentForPreviousGameIsDiscardedByGenerationGate()
    {
        using var fixture = new AssetsFixture();
        var gate = new ContentGenerationGate();
        string? shownTitle = null;

        // 原神请求先发但被网络挂住，用户随后切到绝区零。
        fixture.Handler.Delay(GenshinGameId);
        var genshinGeneration = gate.Next();
        var genshinTask = ApplyAsync(gate, genshinGeneration, GenshinBiz);

        var zzzGeneration = gate.Next();
        await ApplyAsync(gate, zzzGeneration, ZzzBiz);
        Assert.Equal("绝区零公告", shownTitle);

        // 原神（旧代次）的响应晚到，必须被丢弃，不能覆盖绝区零。
        fixture.Handler.Release(GenshinGameId);
        await genshinTask;
        Assert.Equal("绝区零公告", shownTitle);


        async Task ApplyAsync(ContentGenerationGate generationGate, int generation, string gameBiz)
        {
            var content = await fixture.Assets.GetContentAsync(LauncherId.ChinaOfficial, gameBiz);
            if (!generationGate.IsCurrent(generation))
            {
                return;
            }

            shownTitle = content?.Posts.FirstOrDefault(x => x.Type == GamePostType.POST_TYPE_ANNOUNCE)?.Title;
        }
    }


    [Fact]
    public async Task ConcurrentRequestsForSameGameFetchContentOnce()
    {
        using var fixture = new AssetsFixture();
        fixture.Handler.Delay(GenshinGameId);

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => fixture.Assets.GetContentAsync(LauncherId.ChinaOfficial, GenshinBiz))
            .ToArray();

        await fixture.Handler.WaitForContentCallsAsync(GenshinGameId);
        fixture.Handler.Release(GenshinGameId);
        var results = await Task.WhenAll(tasks);

        Assert.All(results, x => Assert.Equal(GenshinGameId, x!.GameId.Id));
        Assert.Equal(1, fixture.Handler.GameInfoCalls);
        Assert.Equal(1, fixture.Handler.ContentCallsFor(GenshinGameId));
    }


    [Fact]
    public async Task GetContentAsyncRejectsContentBelongingToAnotherGame()
    {
        using var fixture = new AssetsFixture();
        fixture.Handler.ContentJsonFactory = gameId => gameId == ZzzGameId
            ? ContentJson(GenshinGameId, GenshinBiz, "原神公告")
            : ContentJson(gameId, BizOf(gameId), TitleOf(gameId));

        // 服务端回错了游戏：宁可不显示，也不能把原神的公告贴到绝区零页面上。
        var content = await fixture.Assets.GetContentAsync(LauncherId.ChinaOfficial, ZzzBiz);

        Assert.Null(content);
    }


    [Fact]
    public async Task GetContentAsyncForUnknownGameReturnsNullWithoutLeakingOtherGames()
    {
        using var fixture = new AssetsFixture();

        var missing = await fixture.Assets.GetContentAsync(LauncherId.ChinaOfficial, StarRailBiz);
        var genshin = await fixture.Assets.GetContentAsync(LauncherId.ChinaOfficial, GenshinBiz);

        Assert.Null(missing);
        Assert.Equal(GenshinGameId, genshin!.GameId.Id);
    }


    [Fact]
    public void ContentGenerationGateOnlyAcceptsLatestGeneration()
    {
        var gate = new ContentGenerationGate();
        var first = gate.Next();
        var second = gate.Next();

        Assert.False(gate.IsCurrent(first));
        Assert.True(gate.IsCurrent(second));
        Assert.False(gate.IsCurrent(first));
    }


    private static string BizOf(string gameId) => gameId switch
    {
        GenshinGameId => GenshinBiz,
        ZzzGameId => ZzzBiz,
        _ => "hkrpg_cn",
    };


    private static string TitleOf(string gameId) => gameId switch
    {
        GenshinGameId => "原神公告",
        ZzzGameId => "绝区零公告",
        _ => "星铁公告",
    };


    private static string ContentJson(string gameId, string biz, string title) => $$"""
        {
          "retcode": 0,
          "message": "OK",
          "data": {
            "content": {
              "game": { "id": "{{gameId}}", "biz": "{{biz}}" },
              "language": "zh-cn",
              "banners": [],
              "posts": [
                {
                  "id": "post-1",
                  "type": "POST_TYPE_ANNOUNCE",
                  "title": "{{title}}",
                  "link": "https://example.invalid/post-1",
                  "date": "01/01"
                }
              ]
            }
          }
        }
        """;


    private sealed class AssetsFixture : IDisposable
    {
        private readonly TempDirectory _temp = new("lunarward-assets-");


        public AssetsFixture()
        {
            var paths = new XdgPaths(_temp.Combine("config"), _temp.Combine("data"), _temp.Combine("cache"));
            Handler = new FakeHoYoPlayHandler();
            Log = new LogService(paths);
            Assets = new StarwardAssets(paths, Log, Handler);
        }


        public FakeHoYoPlayHandler Handler { get; }

        public LogService Log { get; }

        public StarwardAssets Assets { get; }


        public void Dispose()
        {
            Assets.Dispose();
            Log.Dispose();
            _temp.Dispose();
        }
    }


    /// <summary>伪造 hyp-connect 的 <c>getGames</c> / <c>getGameContent</c> 两个接口。</summary>
    private sealed class FakeHoYoPlayHandler : HttpMessageHandler
    {
        private const string GamesJson = """
            {"retcode":0,"message":"OK","data":{"games":[
            {"id":"1Z8W5NHUQb","biz":"hk4e_cn","display":{"language":"zh-cn","name":"原神"}},
            {"id":"T2S0Gz4Dr2","biz":"nap_cn","display":{"language":"zh-cn","name":"绝区零"}}
            ]}}
            """;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _delays = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, int> _contentCallsByGameId = new(StringComparer.Ordinal);
        private int _gameInfoCalls;
        private int _contentCalls;


        public Func<string, string>? ContentJsonFactory { get; set; }

        public int GameInfoCalls => Volatile.Read(ref _gameInfoCalls);

        public int ContentCalls => Volatile.Read(ref _contentCalls);


        public int ContentCallsFor(string gameId)
            => _contentCallsByGameId.TryGetValue(gameId, out var count) ? count : 0;


        /// <summary>让该游戏的内容请求挂住，直到 <see cref="Release"/>。</summary>
        public void Delay(string gameId)
            => _delays[gameId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);


        public void Release(string gameId)
        {
            if (_delays.TryGetValue(gameId, out var gate))
            {
                gate.TrySetResult(true);
            }
        }


        /// <summary>等到该游戏的内容请求确实进了 handler（单飞测试用）。</summary>
        public async Task WaitForContentCallsAsync(string gameId)
        {
            for (var i = 0; i < 200; i++)
            {
                if (ContentCallsFor(gameId) > 0 && _delays.ContainsKey(gameId))
                {
                    return;
                }

                await Task.Delay(10);
            }

            throw new TimeoutException($"等待 {gameId} 的内容请求超时");
        }


        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("getGames", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _gameInfoCalls);
                return Json(GamesJson);
            }

            if (url.Contains("getGameContent", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _contentCalls);
                var gameId = QueryValue(url, "game_id");
                _contentCallsByGameId.AddOrUpdate(gameId, 1, (_, count) => count + 1);
                if (_delays.TryGetValue(gameId, out var gate))
                {
                    await gate.Task.ConfigureAwait(false);
                }

                var json = ContentJsonFactory?.Invoke(gameId) ?? ContentJson(gameId, BizOf(gameId), TitleOf(gameId));
                return Json(json);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }


        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };


        private static string QueryValue(string url, string name)
        {
            var marker = name + "=";
            var index = url.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                return string.Empty;
            }

            var start = index + marker.Length;
            var end = url.IndexOf('&', start);
            return end < 0 ? url[start..] : url[start..end];
        }
    }
}
