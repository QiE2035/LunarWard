using System.Text.Json.Serialization;

namespace LunarWard.Core.HoYoPlay;

public class WPFPackageInfo
{

    [JsonPropertyName("game")]
    public GameId GameId { get; set; }

    [JsonPropertyName("wpf_package")]
    public WPFPackage WPFPackage { get; set; }

}


public class WPFPackage
{

    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;

    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;

    [JsonPropertyName("md5")]
    public string MD5 { get; set; } = null!;

    [JsonPropertyName("size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Size { get; set; }

}