using System.Text.Json.Serialization;

namespace LunarWard.Features.GameSetting;

/// <summary>
/// 游戏分辨率设置（原样搬自 <c>Starward/src/Starward/Features/GameSetting/GraphicsSettings_PCResolution_h431323223.cs</c>）。
/// </summary>
public class GraphicsSettings_PCResolution_h431323223
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("isFullScreen")]
    public bool IsFullScreen { get; set; }
}
