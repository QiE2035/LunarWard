using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.SimulatedUniverse;

public class SimulatedUniverseBuffItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("is_evoluted")]
    public bool IsEvoluted { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}


