using System.Numerics;
using SkiaSharp;

namespace LunarWard.Codec;

/// <summary>
/// 计算逐像素的 HDR/SDR 增益图（gain map）。
/// 对应上游 <c>Starward/Features/Codec/UhdrGainmapEffect.cs</c> 中的 <c>UhdrPixelGainEffect</c>。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游是 ComputeSharp.D2D1
/// <c>UhdrPixelGainShader</c>；本仓库为 CPU Span 内核，公式逐字一致：
/// <c>gain = (max(hdr.rgb,0) + 0.015625) / (max(sdr.rgb,0) + 0.015625)</c>，
/// 输出 <c>float4(gain, sdr.a)</c>（内部为 RgbaF32 位图）。
/// </remarks>
public partial class UhdrPixelGainEffect
{
    /// <summary>ISO 21496-1 规定的偏移量。</summary>
    private const float OFFSET = 0.015625f;

    /// <summary>SDR 参考图（上游是 tone map 后的线性图像，不是编码后的 sRGB 值）。</summary>
    public SKBitmap? SdrSource { get; set; }

    /// <summary>HDR 原图（scRGB 线性浮点）。</summary>
    public SKBitmap? HdrSource { get; set; }

    public CanvasBufferPrecision? BufferPrecision { get; set; }

    /// <summary>计算增益图，返回 RgbaF32 位图。</summary>
    public SKBitmap Apply()
    {
        ArgumentNullException.ThrowIfNull(SdrSource);
        ArgumentNullException.ThrowIfNull(HdrSource);
        var sdr = PixelBitmap.ToFloatPixels(SdrSource);
        var hdr = PixelBitmap.ToFloatPixels(HdrSource);
        int count = SdrSource.Width * SdrSource.Height;
        for (int i = 0; i < count; i++)
        {
            int index = i * 4;
            sdr[index + 0] = (MathF.Max(hdr[index + 0], 0) + OFFSET) / (MathF.Max(sdr[index + 0], 0) + OFFSET);
            sdr[index + 1] = (MathF.Max(hdr[index + 1], 0) + OFFSET) / (MathF.Max(sdr[index + 1], 0) + OFFSET);
            sdr[index + 2] = (MathF.Max(hdr[index + 2], 0) + OFFSET) / (MathF.Max(sdr[index + 2], 0) + OFFSET);
            // alpha 保留 SDR 的
        }
        return PixelBitmap.FromFloatPixels(sdr, SdrSource.Width, SdrSource.Height);
    }
}


/// <summary>
/// 把增益图按内容 boost 范围归一化到 0..1。
/// 对应上游 <c>Starward/Features/Codec/UhdrGainmapEffect.cs</c> 中的 <c>UhdrGainmapEffect</c>。
/// </summary>
/// <remarks>
/// 平台替换同 <see cref="UhdrPixelGainEffect"/>：CPU Span 内核，公式
/// <c>logGain = (log2(gain) - log2(minBoost)) / (log2(maxBoost) - log2(minBoost))</c>
/// 后 clamp 到 0..1，输出 8bit 归一化位图。上游属性类型是 ComputeSharp
/// <c>float3</c>，这里用 <see cref="Vector3"/>（同形状、同语义）。
/// </remarks>
public partial class UhdrGainmapEffect
{
    public Vector3 MinContentBoost { get; set; }

    public Vector3 MaxContentBoost { get; set; }

    public SKBitmap? PixelGainSource { get; set; }

    public CanvasBufferPrecision? BufferPrecision { get; set; }

    /// <summary>计算归一化后的增益图，返回 RgbaF32 位图（编码时再转 8bit JPEG）。</summary>
    public SKBitmap Apply()
    {
        ArgumentNullException.ThrowIfNull(PixelGainSource);
        Vector3 minBoostLog = new(MathF.Log2(MinContentBoost.X), MathF.Log2(MinContentBoost.Y), MathF.Log2(MinContentBoost.Z));
        Vector3 maxBoostLog = new(MathF.Log2(MaxContentBoost.X), MathF.Log2(MaxContentBoost.Y), MathF.Log2(MaxContentBoost.Z));

        var pixels = PixelBitmap.ToFloatPixels(PixelGainSource);
        int count = PixelGainSource.Width * PixelGainSource.Height;
        for (int i = 0; i < count; i++)
        {
            int index = i * 4;
            pixels[index + 0] = Normalize(pixels[index + 0], minBoostLog.X, maxBoostLog.X);
            pixels[index + 1] = Normalize(pixels[index + 1], minBoostLog.Y, maxBoostLog.Y);
            pixels[index + 2] = Normalize(pixels[index + 2], minBoostLog.Z, maxBoostLog.Z);
        }
        return PixelBitmap.FromFloatPixels(pixels, PixelGainSource.Width, PixelGainSource.Height);
    }


    private static float Normalize(float gain, float minBoostLog, float maxBoostLog)
    {
        if (gain <= 0 || maxBoostLog == minBoostLog)
        {
            return 0;
        }
        float logGain = (MathF.Log2(gain) - minBoostLog) / (maxBoostLog - minBoostLog);
        return Math.Clamp(logGain, 0, 1);
    }
}
