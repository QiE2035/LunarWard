using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.SimulatedUniverse;

public class SimulatedUniverseInfo
{
    [JsonPropertyName("role")]
    public SimulatedUniverseGameRole Role { get; set; } = null!;

    [JsonPropertyName("basic_info")]
    public SimulatedUniverseBasicStats BasicInfo { get; set; } = null!;

    [JsonPropertyName("current_record")]
    public SimulatedUniverseRecord CurrentRecord { get; set; } = null!;

    [JsonPropertyName("last_record")]
    public SimulatedUniverseRecord LastRecord { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}


