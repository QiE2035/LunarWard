using System.Numerics;
using SkiaSharp;

namespace LunarWard.Codec;

/// <summary>
/// scRGB 线性浮点 → HDR10（BT.2020 + PQ，16bit 归一化）。
/// 对应上游 <c>Starward/Features/Codec/HDR10GammaEffect.cs</c> 中的 <c>ScRGBToHDR10Effect</c>。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游是 ComputeSharp.D2D1 着色器 +
/// Win2D <c>CanvasEffect</c>，在 GPU 上逐像素执行；本仓库改为 CPU Span 内核，
/// BT.709→BT.2020 矩阵与 PQ OETF 常量逐位照搬上游着色器，输出仍为
/// R16G16B16A16UIntNormalized 位图。像素格式契约与上游一致，只是执行单元从 GPU 变 CPU。
/// </remarks>
public sealed class ScRGBToHDR10Effect
{
    /// <summary>输入 scRGB 线性浮点位图。</summary>
    public SKBitmap? Source { get; set; }

    /// <summary>上游同名属性；CPU 内核固定按 32bit float 计算。</summary>
    public CanvasBufferPrecision? BufferPrecision { get; set; }

    /// <summary>执行转换，返回 16bit 归一化的 HDR10 位图。</summary>
    public SKBitmap Apply()
    {
        ArgumentNullException.ThrowIfNull(Source);
        return Hdr10Transfer.ScRgbToHdr10(Source);
    }
}


/// <summary>
/// HDR10（BT.2020 + PQ，16bit 归一化）→ scRGB 线性浮点。
/// 对应上游 <c>Starward/Features/Codec/HDR10GammaEffect.cs</c> 中的 <c>HDR10ToScRGBEffect</c>。
/// </summary>
/// <remarks>
/// 平台替换同 <see cref="ScRGBToHDR10Effect"/>：着色器改 CPU Span，PQ EOTF 与
/// BT.2020→BT.709 矩阵常量照搬上游；输出 RgbaF32 位图（上游为 R16G16B16A16Float）。
/// </remarks>
public sealed class HDR10ToScRGBEffect
{
    public SKBitmap? Source { get; set; }

    public CanvasBufferPrecision? BufferPrecision { get; set; }

    public SKBitmap Apply()
    {
        ArgumentNullException.ThrowIfNull(Source);
        return Hdr10Transfer.Hdr10ToScRgb(Source);
    }
}


/// <summary>
/// HDR10 传递函数与 BT.2020/BT.709 矩阵的 CPU 实现。
/// </summary>
internal static class Hdr10Transfer
{
    // PQ（SMPTE ST 2084）常量，与上游 PQEOTFShader/PQOETFShader 一致。
    private const float C1 = 107f / 128f;
    private const float C2 = 2413f / 128f;
    private const float C3 = 2392f / 128f;
    private const float M = 2523f / 32f;
    private const float N = 1305f / 8192f;

    /// <summary>上游 PQOETFShader 的 maxLevel 参数（scRGB 参考白 125 nit 对应 1.0）。</summary>
    public const float MaxLevel = 125f;


    /// <summary>
    /// BT.709 线性 → BT.2020 线性。矩阵按 D2D ColorMatrixEffect 的转置布局书写
    /// （与上游 ColorMatrixEffect 常量逐位一致）：out.R = M11*r + M21*g + M31*b。
    /// </summary>
    private static readonly Matrix4x4 Bt709ToBt2020 = new(
        0.6284037f, 0.0644736f, 0.0164828f, 0,
        0.3298326f, 0.9171785f, 0.0880559f, 0,
        0.0433918f, 0.0110605f, 0.8955600f, 0,
        0, 0, 0, 1);

    /// <summary>
    /// BT.2020 线性 → BT.709 线性（上游 HDR10ToScRGBEffect 中的 ColorMatrixEffect 常量）。
    /// </summary>
    private static readonly Matrix4x4 Bt2020ToBt709 = new(
        1.6535364f, -0.1160068f, -0.0190270f, 0,
        -0.5876440f, 1.1328219f, -0.1005690f, 0,
        -0.0728597f, -0.0083700f, 1.1187837f, 0,
        0, 0, 0, 1);


    /// <summary>PQ OETF：线性 scRGB（1.0 = 125nit）→ PQ 编码值。</summary>
    public static float PqOetf(float value)
    {
        float x = MathF.Abs(value / MaxLevel);
        float v1 = C1 + C2 * MathF.Pow(x, N);
        float v2 = 1 + C3 * MathF.Pow(x, N);
        return MathF.Pow(v1 / v2, M);
    }

    /// <summary>PQ EOTF：PQ 编码值 → 线性 scRGB（1.0 = 125nit）。</summary>
    public static float PqEotf(float value)
    {
        float v1 = MathF.Max(MathF.Pow(value, 1 / M) - C1, 0);
        float v2 = C2 - C3 * MathF.Pow(value, 1 / M);
        return MaxLevel * MathF.Pow(MathF.Abs(v1 / v2), 1 / N);
    }


    public static SKBitmap ScRgbToHdr10(SKBitmap source)
    {
        var pixels = PixelBitmap.ToFloatPixels(source);
        int count = source.Width * source.Height;
        for (int i = 0; i < count; i++)
        {
            int index = i * 4;
            float r = pixels[index + 0];
            float g = pixels[index + 1];
            float b = pixels[index + 2];
            float r2020 = Bt709ToBt2020.M11 * r + Bt709ToBt2020.M21 * g + Bt709ToBt2020.M31 * b;
            float g2020 = Bt709ToBt2020.M12 * r + Bt709ToBt2020.M22 * g + Bt709ToBt2020.M32 * b;
            float b2020 = Bt709ToBt2020.M13 * r + Bt709ToBt2020.M23 * g + Bt709ToBt2020.M33 * b;
            pixels[index + 0] = PqOetf(r2020);
            pixels[index + 1] = PqOetf(g2020);
            pixels[index + 2] = PqOetf(b2020);
        }
        return PixelBitmap.FromNormalizedFloatPixels(pixels, source.Width, source.Height);
    }


    public static SKBitmap Hdr10ToScRgb(SKBitmap source)
    {
        var pixels = PixelBitmap.ToFloatPixels(source);
        int count = source.Width * source.Height;
        for (int i = 0; i < count; i++)
        {
            int index = i * 4;
            float r2020 = PqEotf(pixels[index + 0]);
            float g2020 = PqEotf(pixels[index + 1]);
            float b2020 = PqEotf(pixels[index + 2]);
            float r709 = Bt2020ToBt709.M11 * r2020 + Bt2020ToBt709.M21 * g2020 + Bt2020ToBt709.M31 * b2020;
            float g709 = Bt2020ToBt709.M12 * r2020 + Bt2020ToBt709.M22 * g2020 + Bt2020ToBt709.M32 * b2020;
            float b709 = Bt2020ToBt709.M13 * r2020 + Bt2020ToBt709.M23 * g2020 + Bt2020ToBt709.M33 * b2020;
            pixels[index + 0] = r709;
            pixels[index + 1] = g709;
            pixels[index + 2] = b709;
        }
        return PixelBitmap.FromFloatPixels(pixels, source.Width, source.Height);
    }
}
