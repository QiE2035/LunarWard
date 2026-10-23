using System.Text.Json.Serialization;

namespace LunarWard.Core.Gacha.ZZZ;

public class ZZZGachaWiki
{

    [JsonPropertyName("game")]
    public string Game { get; set; }

    [JsonPropertyName("lang")]
    public string Language { get; set; }

    [JsonPropertyName("list")]
    public List<ZZZGachaInfo> List { get; set; }

}
