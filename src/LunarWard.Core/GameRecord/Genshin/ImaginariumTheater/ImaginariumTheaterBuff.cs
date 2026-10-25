using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.Genshin.ImaginariumTheater;

public class ImaginariumTheaterBuff
{

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;


    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;


    [JsonPropertyName("desc")]
    public string Desc { get; set; } = null!;

    /// <summary>
    /// 已强化
    /// </summary>
    [JsonPropertyName("is_enhanced")]
    public bool IsEnhanced { get; set; }


    [JsonPropertyName("id")]
    public int Id { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}

