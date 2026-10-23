using System.Text.Json.Serialization;

namespace LunarWard.Core.GameRecord;


internal class DeviceFpResult
{

    [JsonPropertyName("code")]
    public int Code { get; set; }


    [JsonPropertyName("msg")]
    public string Message { get; set; }


    [JsonPropertyName("device_fp")]
    public string DeviceFp { get; set; }

}
