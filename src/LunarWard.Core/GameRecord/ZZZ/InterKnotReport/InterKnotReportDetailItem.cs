using LunarWard.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.ZZZ.InterKnotReport;

public class InterKnotReportDetailItem
{

    [JsonIgnore]
    public long Uid { get; set; }

    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }

    [JsonIgnore]
    public string DataMonth { get; set; } = null!;

    [JsonIgnore]
    public string DataType { get; set; } = null!;


    [JsonPropertyName("action")]
    public string Action { get; set; } = null!;


    [JsonPropertyName("time")]
    [JsonConverter(typeof(TimestampStringJsonConverter))]
    public DateTimeOffset Time { get; set; }


    [JsonPropertyName("num")]
    public int Number { get; set; }

}
