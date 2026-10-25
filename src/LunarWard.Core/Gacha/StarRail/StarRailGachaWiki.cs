using System.Text.Json.Serialization;

namespace LunarWard.Core.Gacha.StarRail;

public class StarRailGachaWiki
{

    [JsonPropertyName("game")]
    public string Game { get; set; } = null!;

    [JsonIgnore]
    public string Language { get; set; } = null!;

    [JsonPropertyName("avatar")]
    public List<StarRailGachaInfo> Avatar { get; set; } = [];

    [JsonPropertyName("equipment")]
    public List<StarRailGachaInfo> Equipment { get; set; } = [];

}

