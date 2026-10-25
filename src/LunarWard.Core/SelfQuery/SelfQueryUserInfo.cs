using System.Text.Json.Serialization;

namespace LunarWard.Core.SelfQuery;

public class SelfQueryUserInfo
{

    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Uid { get; set; }


    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = null!;


    [JsonPropertyName("region")]
    public string Region { get; set; } = null!;


    [JsonPropertyName("level")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int Level { get; set; }


    [JsonIgnore]
    public GameBiz GameBiz { get; set; }

}
