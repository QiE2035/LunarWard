using System.Text.Json.Serialization;

namespace LunarWard.Core.Gacha.StarRail;

internal class StarRailGachaInfoWrapper
{

    [JsonPropertyName("list")]
    public List<StarRailGachaInfo> List { get; set; }
}

