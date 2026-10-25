using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.ImaginariumTheater;

public class ImaginariumTheaterLinks
{
    [JsonPropertyName("lineup_link")]
    public string LineupLink { get; set; } = null!;

    [JsonPropertyName("lineup_link_pc")]
    public string LineupLinkPc { get; set; } = null!;

    [JsonPropertyName("strategy_link")]
    public string StrategyLink { get; set; } = null!;

    [JsonPropertyName("lineup_publish_link")]
    public string LineupPublishLink { get; set; } = null!;

    [JsonPropertyName("lineup_publish_link_pc")]
    public string LineupPublishLinkPc { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

