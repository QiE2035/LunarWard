using System.Text.Json.Serialization;

namespace LunarWard.Installer;

internal class PkgVersionItem
{

    [JsonPropertyName("remoteName")]
    public string RemoteName { get; set; } = default!;

    [JsonPropertyName("md5")]
    public string MD5 { get; set; } = default!;

    [JsonPropertyName("fileSize")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long FileSize { get; set; }

}



internal class BlacklistItem
{

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = default!;

}