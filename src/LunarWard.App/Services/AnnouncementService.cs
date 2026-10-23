using System.Text.Json;
using LunarWard.Core.GameNotice;
using LunarWard.Core.HoYoPlay;
using LunarWard.Platform;

namespace LunarWard.App.Services;

/// <summary>公告来源。</summary>
public enum AnnouncementSource
{
    /// <summary>官方红点（<c>getAlertAnn</c>）。</summary>
    Alert,

    /// <summary>版本亮点（<c>getAllGameBasicInfo</c>）。</summary>
    Highlight,

    /// <summary>LunarWard 自己的应用内公告。</summary>
    InApp,
}


/// <summary>统一后的公告。</summary>
public sealed record Announcement(
    string Id,
    AnnouncementSource Source,
    string Title,
    string Body,
    string? ImageUrl,
    DateTimeOffset? PublishedAt,
    Uri? ExternalLink,
    bool IsRead);


/// <summary>
/// 公告三源（<c>docs/04-FEATURES.md</c> §5.5）。
/// </summary>
/// <remarks>
/// 全部经过 <c>LunarWard.Core</c> 的现成客户端，不重复实现 API。
/// 网络失败时**必须**静默降级为空列表——公告不是启动路径上的关键依赖。
/// </remarks>
public sealed class AnnouncementService
{
    private readonly IXdgPaths _paths;

    private readonly HttpClient _httpClient;


    public AnnouncementService(IXdgPaths paths, HttpClient? httpClient = null)
    {
        _paths = paths;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }


    /// <summary>红点状态持久化文件。</summary>
    public string StateFile => Path.Join(_paths.DataHome, "announcements.json");


    /// <summary>
    /// 拉取某游戏的公告（红点 + 版本亮点 + 应用内）。
    /// </summary>
    public async Task<IReadOnlyList<Announcement>> FetchAsync(string gameBiz, CancellationToken ct = default)
    {
        var result = new List<Announcement>();
        var read = LoadReadIds();

        // 1) 官方红点（Core 的 IsNoticeAlertAsync 需要 uid；没有 uid 时用 0，官方接口对未登录返回 alert=false）
        try
        {
            var client = new GameNoticeClient(_httpClient);
            var alert = await client.IsNoticeAlertAsync(new LunarWard.Core.GameBiz(gameBiz), 0, "zh-cn", ct).ConfigureAwait(false);
            if (alert)
            {
                var id = $"alert-{gameBiz}";
                result.Add(new Announcement(
                    id, AnnouncementSource.Alert,
                    "有新的游戏公告",
                    "官方发布了新公告，点击查看",
                    null, null,
                    new Uri($"https://sdk.mihoyo.com/hk4e/announcement/index.html?auth_appid=announcement&game_biz={gameBiz}"),
                    read.Contains(id)));
            }
        }
        catch (Exception)
        {
            // 离线/接口变化：跳过这一源
        }

        // 2) 版本亮点（背景/视频信息也在这里）
        try
        {
            var client = new HoYoPlayClient(_httpClient);
            var launcherId = LauncherId.FromGameBiz(new LunarWard.Core.GameBiz(gameBiz));
            if (launcherId is not null)
            {
                var infos = await client.GetGameBackgroundAsync(launcherId, "zh-cn", ct).ConfigureAwait(false);

                // 只保留"当前游戏"的条目：launcher 返回的是该启动器下全部游戏
                var wanted = LunarWard.Core.HoYoPlay.GameId.FromGameBiz(new LunarWard.Core.GameBiz(gameBiz));
                foreach (var info in (infos ?? []).Where(x => wanted is null || x.GameId == wanted))
                {
                    if (info.GameId is null)
                    {
                        continue;
                    }

                    foreach (var bg in info.Backgrounds ?? [])
                    {
                        var url = bg.Background?.Url;
                        var id = $"hl-{info.GameId?.Id}-{bg.Id}";
                        var isVideo = string.Equals(bg.Type, GameBackground.BACKGROUND_TYPE_VIDEO, StringComparison.Ordinal);
                        result.Add(new Announcement(
                            id, AnnouncementSource.Highlight,
                            "版本亮点",
                            isVideo ? "本版本提供视频背景" : "本版本提供静态背景",
                            url,
                            null,
                            string.IsNullOrEmpty(bg.Background?.Link) ? null : new Uri(bg.Background!.Link),
                            read.Contains(id)));

                        // 视频背景信息（供 BackgroundService 下载与缓存）
                        if (isVideo && !string.IsNullOrEmpty(bg.Video?.Url))
                        {
                            result.Add(new Announcement(
                                id + "-video", AnnouncementSource.Highlight,
                                "版本视频背景",
                                bg.Video!.Url,
                                bg.Video.Url,
                                null, null,
                                read.Contains(id + "-video")));
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // 离线：跳过
        }

        // 3) 应用内公告
        foreach (var item in LoadInApp())
        {
            result.Add(item with { IsRead = read.Contains(item.Id) });
        }

        // 去重（同一 id 只留一次；同一来源同一游戏只留一条）并限制总量，避免首屏被刷屏
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var deduped = new List<Announcement>();
        foreach (var item in result)
        {
            if (!seenIds.Add(item.Id))
            {
                continue;
            }

            var key = $"{item.Source}|{GameKey(item)}";
            if (!seenKeys.Add(key))
            {
                continue;
            }

            deduped.Add(item);
            if (deduped.Count >= 12)
            {
                break;
            }
        }

        return deduped;
    }


    private static string GameKey(Announcement announcement)
    {
        // hl-<gameId>-<bgId> / hl-<gameId>-<bgId>-video → 同一个 key
        var id = announcement.Id;
        var dash = id.IndexOf('-', 3);
        return dash > 0 ? id[..dash] : id;
    }


    /// <summary>标记已读并持久化。</summary>
    public void MarkRead(string id)
    {
        var read = LoadReadIds();
        if (!read.Add(id))
        {
            return;
        }

        try
        {
            AtomicFile.WriteAllText(StateFile, JsonSerializer.Serialize(read.ToArray()));
        }
        catch (Exception)
        {
            // ignore
        }
    }


    private HashSet<string> LoadReadIds()
    {
        try
        {
            if (!File.Exists(StateFile))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var ids = JsonSerializer.Deserialize<string[]>(File.ReadAllText(StateFile)) ?? [];
            return new HashSet<string>(ids, StringComparer.Ordinal);
        }
        catch (Exception)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }


    /// <summary>应用内公告（本地 JSON，随包分发或用户放置）。</summary>
    private IReadOnlyList<Announcement> LoadInApp()
    {
        var path = Path.Join(_paths.DataHome, "in-app-announcements.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<InAppAnnouncement>>(File.ReadAllText(path)) ?? [];
            return items.Select(x => new Announcement(
                "app-" + (x.Id ?? x.Title),
                AnnouncementSource.InApp,
                x.Title ?? "",
                x.Body ?? "",
                x.ImageUrl,
                x.PublishedAt,
                x.Link is null ? null : new Uri(x.Link),
                false)).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }


    private sealed class InAppAnnouncement
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Body { get; set; }

        public string? ImageUrl { get; set; }

        public DateTimeOffset? PublishedAt { get; set; }

        public string? Link { get; set; }
    }
}
