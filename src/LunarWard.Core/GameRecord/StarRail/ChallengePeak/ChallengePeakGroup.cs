using LunarWard.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.ChallengePeak;

public class ChallengePeakGroup
{
    [JsonPropertyName("group_id")]
    public int GroupId { get; set; }

    [JsonPropertyName("begin_time")]
    [JsonConverter(typeof(DateTimeObjectJsonConverter))]
    public DateTime BeginTime { get; set; }

    [JsonPropertyName("end_time")]
    [JsonConverter(typeof(DateTimeObjectJsonConverter))]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    [JsonPropertyName("name_mi18n")]
    public string NameMi18n { get; set; } = null!;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = null!;

    [JsonPropertyName("theme_pic_path")]
    public string ThemePicPath { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
