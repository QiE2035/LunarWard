using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.ThresholdSimulation;

/// <summary>
/// buff
/// </summary>
public class ThresholdSimulationBuff
{

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}
