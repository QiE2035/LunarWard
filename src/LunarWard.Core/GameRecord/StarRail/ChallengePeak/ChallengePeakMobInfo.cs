using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.ChallengePeak;

public class ChallengePeakMobInfo
{
    [JsonPropertyName("maze_id")]
    public int MazeId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("monster_name")]
    public string MonsterName { get; set; } = null!;

    [JsonPropertyName("monster_icon")]
    public string MonsterIcon { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
