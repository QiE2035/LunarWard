using System.Text.Json.Serialization;

namespace LunarWard.Core.HoYoPlay;


/// <summary>
/// 渠道服 SDK
/// </summary>
public class GameChannelSDK
{

    [JsonPropertyName("game")]
    public GameId GameId { get; set; } = null!;


    /// <summary>
    /// SDK 版本
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;


    [JsonPropertyName("channel_sdk_pkg")]
    public GameChannelSDKPackage ChannelSDKPackage { get; set; } = null!;


    [JsonPropertyName("pkg_version_file_name")]
    public string PkgVersionFileName { get; set; } = null!;


}


public class GameChannelSDKPackage
{

    /// <summary>
    /// 下载链接
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;


    [JsonPropertyName("md5")]
    public string MD5 { get; set; } = null!;


    [JsonPropertyName("size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Size { get; set; }


    [JsonPropertyName("decompressed_size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long DecompressedSize { get; set; }

}
