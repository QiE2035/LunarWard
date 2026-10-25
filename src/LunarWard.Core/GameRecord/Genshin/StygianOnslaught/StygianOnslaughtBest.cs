using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.StygianOnslaught;

public class StygianOnslaughtBest
{

    [JsonPropertyName("difficulty")]
    public int Difficulty { get; set; }


    [JsonPropertyName("second")]
    public int Seconds { get; set; }


    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}