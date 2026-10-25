using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord.StarRail.TrailblazeCalendar;

/// <summary>
/// 开拓月历总结
/// </summary>
public class TrailblazeCalendarSummary : IJsonOnDeserialized
{

    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Uid { get; set; }


    [JsonPropertyName("region")]
    public string Region { get; set; } = null!;


    [JsonPropertyName("login_flag")]
    public bool LoginFlag { get; set; }

    /// <summary>
    /// 202304
    /// </summary>
    [JsonPropertyName("optional_month")]
    public List<string> OptionalMonth { get; set; }

    /// <summary>
    /// 发出请求时的月份 202306
    /// </summary>
    [JsonPropertyName("month")]
    public string Month { get; set; } = null!;

    /// <summary>
    /// 返回数据的月份 202304
    /// </summary>
    [JsonPropertyName("data_month")]
    public string DataMonth { get; set; } = null!;

    /// <summary>
    /// 月数据
    /// </summary>
    [JsonPropertyName("month_data")]
    public TrailblazeCalendarMonthData MonthData { get; set; }

    /// <summary>
    /// 日数据
    /// </summary>
    [JsonPropertyName("day_data")]
    public TrailblazeCalendarDayData DayData { get; set; }

    /// <summary>
    /// 发出请求时的游戏版本
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;

    /// <summary>
    /// 202304
    /// </summary>
    [JsonPropertyName("start_month")]
    public string StartMonth { get; set; } = null!;


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }


    public void OnDeserialized()
    {
        MonthData.Uid = Uid;
        MonthData.Month = DataMonth;
    }
}



