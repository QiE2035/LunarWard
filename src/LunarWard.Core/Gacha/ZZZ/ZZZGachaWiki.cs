using System.Text.Json.Serialization;

namespace LunarWard.Core.Gacha.ZZZ;

public class ZZZGachaWiki
{

    [JsonPropertyName("game")]
    public string Game { get; set; } = null!;

    [JsonPropertyName("lang")]
    public string Language { get; set; } = null!;

    [JsonPropertyName("list")]
    public List<ZZZGachaInfo> List { get; set; } = [];

}
