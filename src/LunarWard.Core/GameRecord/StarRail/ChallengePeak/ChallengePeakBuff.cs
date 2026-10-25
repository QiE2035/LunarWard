using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.ChallengePeak;

public class ChallengePeakBuff
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name_mi18n")]
    public string NameMi18n { get; set; } = null!;

    [JsonPropertyName("desc_mi18n")]
    public string DescMi18n { get; set; } = null!;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}