using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.PureFiction;

public class PureFictionBuff
{

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name_mi18n")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("desc_mi18n")]
    public string Desc { get; set; } = null!;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}



