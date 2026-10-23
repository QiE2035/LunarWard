using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.ImaginariumTheater;

public class ImaginariumTheaterEnemy
{

    [JsonPropertyName("name")]
    public string Name { get; set; }


    [JsonPropertyName("icon")]
    public string Icon { get; set; }


    [JsonPropertyName("level")]
    public int Level { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}

