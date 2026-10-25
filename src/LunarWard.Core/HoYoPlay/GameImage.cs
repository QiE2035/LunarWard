using System.Text.Json.Serialization;

namespace LunarWard.Core.HoYoPlay;

/// <summary>
/// 图片、带有链接的图片
/// </summary>
public class GameImage
{

    /// <summary>
    /// 图片链接
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;

    [JsonPropertyName("hover_url")]
    public string? HoverUrl { get; set; } = null!;

    /// <summary>
    /// 点击图片后打开的链接
    /// </summary>
    [JsonPropertyName("link")]
    public string Link { get; set; } = null!;

    [JsonPropertyName("login_state_in_link")]
    public bool LoginStateInLink { get; set; }

}
