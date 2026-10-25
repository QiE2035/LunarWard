using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.ThresholdSimulation;

public class ThresholdSimulationBossInfo
{

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("race_icon")]
    public string RaceIco { get; set; } = null!;

    [JsonPropertyName("bg_icon")]
    public string BgIcon { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}
