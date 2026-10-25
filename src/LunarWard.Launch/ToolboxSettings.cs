using System.Text.Json.Serialization;

namespace LunarWard.Launch;

/// <summary>
/// 工具箱（战绩 API）的本地状态。
/// </summary>
/// <remarks>
/// 对应上游 <c>AppConfig.HyperionDeviceId / HyperionDeviceFp / HyperionDeviceFpLastUpdateTime</c>：
/// 米游社战绩接口按「设备指纹」做风控，换指纹能解除部分限流。上游仅在国服
/// （<c>HyperionClient</c>）下生效，HoYoLAB 直接返回；LunarWard 沿用同一条件。
/// </remarks>
public sealed class ToolboxSettings
{
    /// <summary>设备 ID（x-rpc-device-id），空 = 未获取过。</summary>
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = "";

    /// <summary>设备指纹（x-rpc-device-fp），空 = 未获取过。</summary>
    [JsonPropertyName("device_fp")]
    public string DeviceFp { get; set; } = "";

    /// <summary>上次获取时间；超过 3 天上游会重新更新。</summary>
    [JsonPropertyName("device_fp_last_update")]
    public DateTimeOffset? DeviceFpLastUpdate { get; set; }
}
