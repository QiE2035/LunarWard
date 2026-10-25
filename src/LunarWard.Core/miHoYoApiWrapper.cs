using System.Text.Json.Serialization;

namespace LunarWard.Core;

public class miHoYoApiWrapper<T>
{

    [JsonPropertyName("retcode")]
    public int Retcode { get; set; }


    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;


    [JsonPropertyName("data")]
    public T Data { get; set; }
}