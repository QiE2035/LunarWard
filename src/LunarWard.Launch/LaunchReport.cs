using System.Text.Json;
using System.Text.Json.Serialization;

namespace LunarWard.Launch;

/// <summary>
/// 启动报告。排障的核心，记录完整个解析链条。
/// </summary>
/// <remarks>见 <c>docs/02-ARCHITECTURE.md</c> §八。</remarks>
public sealed class LaunchReport
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };


    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    [JsonPropertyName("profile")]
    public string Profile { get; set; } = "";

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("exit_code")]
    public int ExitCode { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("phases")]
    public List<ReportPhase> Phases { get; set; } = [];

    [JsonPropertyName("runtime")]
    public ReportRuntime? Runtime { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string> Env { get; set; } = [];

    [JsonPropertyName("env_sources")]
    public Dictionary<string, string> EnvSources { get; set; } = [];

    [JsonPropertyName("command")]
    public List<string> Command { get; set; } = [];

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("game_directory")]
    public string? GameDirectory { get; set; }

    [JsonPropertyName("game_pid")]
    public int? GamePid { get; set; }

    [JsonPropertyName("ready_signal")]
    public string? ReadySignal { get; set; }

    [JsonPropertyName("external_programs")]
    public List<ReportExternalProgram> ExternalPrograms { get; set; } = [];

    [JsonPropertyName("game_exit_code")]
    public int? GameExitCode { get; set; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    [JsonPropertyName("exited_at")]
    public DateTimeOffset? ExitedAt { get; set; }


    public sealed class ReportPhase
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("started")]
        public DateTimeOffset Started { get; set; }

        [JsonPropertyName("duration_ms")]
        public long DurationMs { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }
    }


    public sealed class ReportRuntime
    {
        [JsonPropertyName("runner")]
        public ReportRunner? Runner { get; set; }

        [JsonPropertyName("dxvk")]
        public ReportDxvk? Dxvk { get; set; }

        [JsonPropertyName("prefix")]
        public string? Prefix { get; set; }

        [JsonPropertyName("explain")]
        public string? Explain { get; set; }
    }


    public sealed class ReportRunner
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "";

        [JsonPropertyName("component")]
        public string? Component { get; set; }

        [JsonPropertyName("resolved")]
        public string Resolved { get; set; } = "";
    }


    public sealed class ReportDxvk
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "";

        [JsonPropertyName("component")]
        public string? Component { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }


    public sealed class ReportExternalProgram
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("trigger")]
        public string Trigger { get; set; } = "";

        [JsonPropertyName("started_after")]
        public string StartedAfter { get; set; } = "";

        [JsonPropertyName("pid")]
        public int? Pid { get; set; }

        [JsonPropertyName("exit_code")]
        public int? ExitCode { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }


    /// <summary>从一次启动的计划与结果构建报告。</summary>
    public static LaunchReport From(LaunchPlan plan, LaunchResult result)
    {
        var report = new LaunchReport
        {
            Timestamp = DateTimeOffset.Now,
            Profile = plan.ProfileId,
            Success = result.Success,
            ExitCode = result.ExitCode,
            Error = result.Error?.Message,
            Phases = result.Phases.Select(p => new ReportPhase
            {
                Name = p.Name,
                Started = p.Started,
                DurationMs = p.DurationMs,
                Detail = p.Detail,
            }).ToList(),
            Runtime = new ReportRuntime
            {
                Runner = new ReportRunner
                {
                    Kind = plan.Runtime.Runner.KindName,
                    Component = plan.Runtime.Runner is Wine.RunnerSource.Managed m ? m.Component : null,
                    Resolved = plan.Runtime.WineBinary,
                },
                Dxvk = new ReportDxvk
                {
                    Kind = plan.Runtime.Dxvk.KindName,
                    Component = plan.Runtime.Dxvk is Wine.DxvkSource.Managed dm ? dm.Component : null,
                    Path = plan.Runtime.DxvkDirectory,
                },
                Prefix = plan.Prefix,
                Explain = plan.Runtime.Explain,
            },
            Env = new Dictionary<string, string>(plan.Env, StringComparer.Ordinal),
            EnvSources = new Dictionary<string, string>(plan.EnvSources, StringComparer.Ordinal),
            Command = plan.BuildArgumentVector().ToList(),
            Prefix = plan.Prefix,
            GameDirectory = plan.GameDirectory,
            GamePid = result.GamePid ?? plan.GamePid,
            ReadySignal = result.Signal?.ToString(),
            ExternalPrograms = result.ExternalPrograms.Select(p => new ReportExternalProgram
            {
                Id = p.Id,
                Trigger = p.Trigger.ToString(),
                StartedAfter = p.StartedAfter,
                Pid = p.Pid,
                ExitCode = p.ExitCode,
                Error = p.Error,
            }).ToList(),
            GameExitCode = result.GameExitCode,
            StartedAt = result.StartedAt,
            ExitedAt = result.ExitedAt,
        };
        return report;
    }


    public string ToJson() => JsonSerializer.Serialize(this, Options);


    /// <summary>原子写入 <c>launch-report.json</c>。</summary>
    public void WriteTo(string path) => Platform.AtomicFile.WriteAllText(path, ToJson());


    public static LaunchReport? ReadFrom(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<LaunchReport>(File.ReadAllText(path), Options) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
