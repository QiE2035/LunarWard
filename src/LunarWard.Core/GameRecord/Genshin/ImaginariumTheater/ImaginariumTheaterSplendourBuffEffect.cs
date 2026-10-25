using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.ImaginariumTheater;

public class ImaginariumTheaterSplendourBuffEffect
{

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;


    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;


    [JsonPropertyName("desc")]
    public string Desc { get; set; } = null!;

}