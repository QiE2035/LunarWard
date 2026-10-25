using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord;


public class GameRecordRole
{

    [JsonPropertyName("game_uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Uid { get; set; }


    [JsonPropertyName("game_biz")]
    public string GameBiz { get; set; } = null!;


    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = null!;


    [JsonPropertyName("level")]
    public int Level { get; set; }


    [JsonPropertyName("region")]
    public string Region { get; set; } = null!;


    [JsonPropertyName("region_name")]
    public string RegionName { get; set; } = null!;


    [JsonIgnore]
    public string? Cookie { get; set; } = null!;

    [JsonIgnore]
    public string HeadIcon { get; set; } = null!;

}


internal class GameRecordRoleWrapper
{
    [JsonPropertyName("list")]
    public List<GameRecordRole> List { get; set; }
}