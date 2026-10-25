using LunarWard.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.ApocalypticShadow;

public class ApocalypticShadowMeta
{

    [JsonPropertyName("schedule_id")]
    public int ScheduleId { get; set; }

    [JsonPropertyName("begin_time")]
    [JsonConverter(typeof(DateTimeObjectJsonConverter))]
    public DateTime BeginTime { get; set; }

    [JsonPropertyName("end_time")]
    [JsonConverter(typeof(DateTimeObjectJsonConverter))]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    [JsonPropertyName("name_mi18n")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("upper_boss")]
    public ApocalypticShadowBossMeta UpperBoss { get; set; } = null!;

    [JsonPropertyName("lower_boss")]
    public ApocalypticShadowBossMeta LowerBoss { get; set; } = null!;

    [JsonPropertyName("tierce_boss")]
    public ApocalypticShadowBossMeta? TierceBoss { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}



