using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.ImaginariumTheater;

public class ImaginariumTheaterSplendourBuffItem
{

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;


    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;


    [JsonPropertyName("level")]
    public int Level { get; set; }

    /// <summary>
    /// 祝福不同等级的效果
    /// </summary>
    [JsonPropertyName("level_effect")]
    public List<ImaginariumTheaterSplendourBuffEffect> LevelEffect { get; set; } = [];

}
