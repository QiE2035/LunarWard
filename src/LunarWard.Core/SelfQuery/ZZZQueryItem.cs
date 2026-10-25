using LunarWard.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace LunarWard.Core.SelfQuery;

public class ZZZQueryItem
{

    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Uid { get; set; }


    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Id { get; set; }


    [JsonPropertyName("type")]
    public ZZZQueryType Type { get; set; }


    [JsonPropertyName("reason")]
    public string Reason { get; set; } = null!;


    [JsonPropertyName("add_num")]
    public long AddNum { get; set; }


    [JsonPropertyName("datetime")]
    [JsonConverter(typeof(DateTimeStringJsonConverter))]
    public DateTime DateTime { get; set; }


    [JsonPropertyName("equip_name")]
    public string EquipName { get; set; } = null!;


    [JsonPropertyName("equip_rarity")]
    public int EquipRarity { get; set; }


    [JsonPropertyName("equip_level")]
    public int EquipLevel { get; set; }


    [JsonPropertyName("weapon_name")]
    public string WeaponName { get; set; } = null!;


    [JsonPropertyName("weapon_rarity")]
    public int WeaponRarity { get; set; }


    [JsonPropertyName("weapon_level")]
    public int WeaponLevel { get; set; }


    [JsonPropertyName("client_ip")]
    public string ClientIp { get; set; } = null!;


    [JsonPropertyName("action_name")]
    public string ActionName { get; set; } = null!;


    [JsonPropertyName("card_type")]
    public int CardType { get; set; }


    [JsonPropertyName("item_name")]
    public string ItemName { get; set; } = null!;


}


