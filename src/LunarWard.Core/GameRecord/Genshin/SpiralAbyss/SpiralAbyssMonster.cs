using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.SpiralAbyss;

public class SpiralAbyssMonster
{

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;

    [JsonPropertyName("level")]
    public int Level { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}
