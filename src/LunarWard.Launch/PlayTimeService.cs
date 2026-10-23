using System.Text.Json;
using System.Text.Json.Serialization;
using LunarWard.Platform;

namespace LunarWard.Launch;

/// <summary>一次游戏会话。</summary>
public sealed record PlaySession(
    string ProfileId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int? GameExitCode,
    string? ReadySignal)
{
    [JsonIgnore]
    public TimeSpan Duration => EndedAt - StartedAt;
}


/// <summary>
/// 游戏时长统计（放在 Launch 层：它只消费 <see cref="LaunchResult"/>，不依赖 UI）。
/// </summary>
/// <remarks>
/// <para>
/// 见 <c>docs/05-ROADMAP.md</c> §3.3："游戏时长 —— 复用相位引擎的起止时间"。
/// 相位引擎天然有精确的 <see cref="LaunchResult.StartedAt"/> /
/// <see cref="LaunchResult.ExitedAt"/>，不需要额外的进程监视。
/// </para>
/// <para>
/// 存储：<c>$XDG_DATA_HOME/lunarward/playtime.json</c>，原子写、未知字段保留。
/// </para>
/// </remarks>
public sealed class PlayTimeService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };


    private readonly IXdgPaths _paths;

    private readonly object _gate = new();

    private PlayTimeStore _store;


    public PlayTimeService(IXdgPaths paths)
    {
        _paths = paths;
        _store = Load();
    }


    public string StorePath => Path.Join(_paths.DataHome, "playtime.json");


    /// <summary>全部会话（按开始时间倒序）。</summary>
    public IReadOnlyList<PlaySession> Sessions
    {
        get
        {
            lock (_gate)
            {
                return _store.Sessions.OrderByDescending(x => x.StartedAt).ToList();
            }
        }
    }


    /// <summary>某 profile 的总时长。</summary>
    public TimeSpan TotalFor(string profileId)
    {
        lock (_gate)
        {
            return TimeSpan.FromTicks(
                _store.Sessions.Where(x => x.ProfileId == profileId).Sum(x => x.Duration.Ticks));
        }
    }


    /// <summary>某 profile 最近一次会话。</summary>
    public PlaySession? LastFor(string profileId)
    {
        lock (_gate)
        {
            return _store.Sessions.Where(x => x.ProfileId == profileId)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefault();
        }
    }


    /// <summary>记录一次会话（只记录成功且有起止时间的）。</summary>
    public void Record(string profileId, LaunchResult result)
    {
        if (result.StartedAt is not { } started || result.ExitedAt is not { } exited)
        {
            return;
        }

        if (exited <= started)
        {
            return;
        }

        lock (_gate)
        {
            _store.Sessions.Add(new PlaySession(
                profileId, started, exited, result.GameExitCode, result.Signal?.ToString()));

            // 上限 5000 条；再多对统计没有意义
            if (_store.Sessions.Count > 5000)
            {
                _store.Sessions = _store.Sessions
                    .OrderByDescending(x => x.StartedAt)
                    .Take(5000)
                    .ToList();
            }

            Save();
        }
    }


    private PlayTimeStore Load()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                return JsonSerializer.Deserialize<PlayTimeStore>(File.ReadAllText(StorePath), Options)
                       ?? new PlayTimeStore();
            }
        }
        catch (JsonException)
        {
            // 损坏：备份后重建
            try
            {
                File.Move(StorePath, $"{StorePath}.bad-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", overwrite: true);
            }
            catch (Exception)
            {
                // ignore
            }
        }

        return new PlayTimeStore();
    }


    private void Save()
    {
        try
        {
            AtomicFile.WriteAllText(StorePath, JsonSerializer.Serialize(_store, Options));
        }
        catch (Exception)
        {
            // 统计失败不影响启动
        }
    }


    private sealed class PlayTimeStore
    {
        public int SchemaVersion { get; set; } = 1;

        public List<PlaySession> Sessions { get; set; } = [];

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
