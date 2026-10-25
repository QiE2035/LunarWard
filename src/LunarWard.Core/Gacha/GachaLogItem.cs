using LunarWard.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace LunarWard.Core.Gacha;

public class GachaLogItem
{

    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Uid { get; set; }


    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }


    [JsonPropertyName("gacha_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int GachaType { get; set; }


    [JsonPropertyName("name")]
    public string Name { get; set; }


    [JsonPropertyName("item_type")]
    public string ItemType { get; set; }


    [JsonPropertyName("rank_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int RankType { get; set; }


    [JsonPropertyName("time")]
    [JsonConverter(typeof(DateTimeStringJsonConverter))]
    public DateTime Time { get; set; }


    [JsonPropertyName("item_id")]
    [JsonConverter(typeof(GachaItemIdJsonConverter))]
    public int ItemId { get; set; }


    /// <summary>
    /// 是否 UP 角色/武器（绝区零与星穹铁道的接口都会返回 <c>is_up</c>：1 是、0 否）。
    /// 原神的老接口没有该字段，保持默认 0。
    /// </summary>
    [JsonPropertyName("is_up")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int IsUp { get; set; }


    [JsonPropertyName("count")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int Count { get; set; }


    [JsonPropertyName("lang")]
    public string Lang { get; set; }



    public virtual IGachaType GetGachaType() => new UndefinedGachaType(GachaType);


    public virtual GachaLogItem Clone() => (GachaLogItem)MemberwiseClone();


}
