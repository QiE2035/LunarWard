using System.Numerics;
using SkiaSharp;

namespace LunarWard.Codec;

/// <summary>
/// 图像解码结果。对应上游 <c>Starward/Features/Codec/ImageInfo.cs</c>。
/// </summary>
/// <remarks>
/// 平台替换（登记见 <c>docs/09-LINUX-DIVERGENCES.md</c>）：上游像素容器是
/// <c>Microsoft.Graphics.Canvas.CanvasBitmap</c>（Win2D），本仓库没有 Win2D，改用
/// <c>SkiaSharp.SKBitmap</c> 承载同一批像素格式（8bit RGBA / 16bit RGBA / float RGBA）。
/// 属性名、类型语义（位图 + 是否 HDR + 色彩原色 + ICC）与上游一致。
/// </remarks>
internal class ImageInfo : IDisposable
{
    /// <summary>
    /// 解码后的位图。像素格式见 <see cref="DirectXPixelFormat"/> 与
    /// <see cref="PixelBitmap"/> 的映射表。
    /// </summary>
    public SKBitmap CanvasBitmap { get; set; } = null!;

    /// <summary>
    /// 是否为 HDR10（BT.2020 + PQ + 16bit）内容。为 true 时
    /// <see cref="CanvasBitmap"/> 已被转换为 scRGB 线性浮点位图。
    /// </summary>
    public bool HDR { get; set; }

    /// <summary>
    /// 图像的色彩原色。
    /// </summary>
    public ICC.ColorPrimaries ColorPrimaries { get; set; } = ICC.ColorPrimaries.BT709;

    /// <summary>
    /// 图像内嵌的 ICC 配置文件（若存在）。上游在 AVIF/JXL/PNG 路径上原样保存。
    /// </summary>
    public byte[]? IccData { get; set; }

    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
            }
            CanvasBitmap?.Dispose();
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}


/// <summary>
/// 像素格式枚举。对应 <c>Windows.Graphics.DirectX.DirectXPixelFormat</c> 中
/// 上游实际用到的成员，数值与 DXGI 定义一致。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：WinRT 的
/// <c>Windows.Graphics.DirectX.DirectXPixelFormat</c> 在 Linux 上不存在，
/// 这里定义同名枚举保留 <see cref="ImageSaver"/> 的公开签名形态。
/// </remarks>
public enum DirectXPixelFormat
{
    R32G32B32A32Float = 2,
    R16G16B16A16Float = 10,
    R16G16B16A16UIntNormalized = 11,
    R8G8B8A8UIntNormalized = 28,
    B8G8R8A8UIntNormalized = 87,
}


/// <summary>
/// SkiaSharp 位图与上游 Win2D 像素格式之间的转换与逐像素访问工具。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：SkiaSharp 3.x 已移除
/// <c>SKColorSpaceXform</c>，且本机 Skia 构建不支持 16bit PNG 解码
/// （<c>SKCodec.GetPixels</c> 对 Rgba16161616 返回 InvalidConversion），
/// 因此像素读写一律走 <see cref="SKBitmap.GetPixelSpan"/> 的原始字节，
/// 由本类按 <see cref="SKColorType"/> 做显式解包，不依赖 Skia 的格式转换。
/// </remarks>
internal static class PixelBitmap
{
    public static SKImageInfo GetImageInfo(int width, int height, SKColorType colorType)
        => new(width, height, colorType, SKAlphaType.Unpremul);

    public static SKBitmap Create(int width, int height, SKColorType colorType)
        => new(GetImageInfo(width, height, colorType));

    public static SKColorType ToColorType(DirectXPixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            DirectXPixelFormat.R8G8B8A8UIntNormalized => SKColorType.Rgba8888,
            DirectXPixelFormat.B8G8R8A8UIntNormalized => SKColorType.Bgra8888,
            DirectXPixelFormat.R16G16B16A16UIntNormalized => SKColorType.Rgba16161616,
            DirectXPixelFormat.R16G16B16A16Float => SKColorType.RgbaF16,
            DirectXPixelFormat.R32G32B32A32Float => SKColorType.RgbaF32,
            _ => throw new NotSupportedException($"Unsupported pixel format: {pixelFormat}"),
        };
    }

    public static DirectXPixelFormat ToPixelFormat(SKColorType colorType)
    {
        return colorType switch
        {
            SKColorType.Rgba8888 => DirectXPixelFormat.R8G8B8A8UIntNormalized,
            SKColorType.Bgra8888 => DirectXPixelFormat.B8G8R8A8UIntNormalized,
            SKColorType.Rgba16161616 => DirectXPixelFormat.R16G16B16A16UIntNormalized,
            SKColorType.RgbaF16 => DirectXPixelFormat.R16G16B16A16Float,
            SKColorType.RgbaF16Clamped => DirectXPixelFormat.R16G16B16A16Float,
            SKColorType.RgbaF32 => DirectXPixelFormat.R32G32B32A32Float,
            _ => throw new NotSupportedException($"Unsupported color type: {colorType}"),
        };
    }

    /// <summary>
    /// 是否为浮点（scRGB）位图，等价于上游
    /// <c>format is R16G16B16A16Float or R32G32B32A32Float</c>。
    /// </summary>
    public static bool IsFloat(SKColorType colorType)
        => colorType is SKColorType.RgbaF16 or SKColorType.RgbaF16Clamped or SKColorType.RgbaF32;

    public static SKBitmap FromBytes(ReadOnlySpan<byte> pixels, int width, int height, DirectXPixelFormat pixelFormat)
    {
        var bitmap = Create(width, height, ToColorType(pixelFormat));
        pixels[..Math.Min(pixels.Length, bitmap.ByteCount)].CopyTo(bitmap.GetPixelSpan());
        return bitmap;
    }

    public static byte[] GetBytes(SKBitmap bitmap)
    {
        return bitmap.GetPixelSpan().ToArray();
    }

    /// <summary>
    /// 把任意受支持格式的位图读成 RGBA float 数组（颜色分量不做归一化差异处理：
    /// 8bit → /255，16bit → /65535，float → 原值）。scRGB 线性值就在 float 位图里。
    /// </summary>
    public static float[] ToFloatPixels(SKBitmap bitmap)
    {
        int count = bitmap.Width * bitmap.Height;
        var result = new float[count * 4];
        var bytes = bitmap.GetPixelSpan();
        switch (bitmap.ColorType)
        {
            case SKColorType.Rgba8888:
                for (int i = 0; i < count; i++)
                {
                    result[i * 4 + 0] = bytes[i * 4 + 0] / 255f;
                    result[i * 4 + 1] = bytes[i * 4 + 1] / 255f;
                    result[i * 4 + 2] = bytes[i * 4 + 2] / 255f;
                    result[i * 4 + 3] = bytes[i * 4 + 3] / 255f;
                }
                break;
            case SKColorType.Bgra8888:
                for (int i = 0; i < count; i++)
                {
                    result[i * 4 + 0] = bytes[i * 4 + 2] / 255f;
                    result[i * 4 + 1] = bytes[i * 4 + 1] / 255f;
                    result[i * 4 + 2] = bytes[i * 4 + 0] / 255f;
                    result[i * 4 + 3] = bytes[i * 4 + 3] / 255f;
                }
                break;
            case SKColorType.Rgba16161616:
                {
                    var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ushort>(bytes);
                    for (int i = 0; i < count; i++)
                    {
                        result[i * 4 + 0] = samples[i * 4 + 0] / 65535f;
                        result[i * 4 + 1] = samples[i * 4 + 1] / 65535f;
                        result[i * 4 + 2] = samples[i * 4 + 2] / 65535f;
                        result[i * 4 + 3] = samples[i * 4 + 3] / 65535f;
                    }
                }
                break;
            case SKColorType.RgbaF16:
            case SKColorType.RgbaF16Clamped:
                {
                    var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Half>(bytes);
                    for (int i = 0; i < count; i++)
                    {
                        result[i * 4 + 0] = (float)samples[i * 4 + 0];
                        result[i * 4 + 1] = (float)samples[i * 4 + 1];
                        result[i * 4 + 2] = (float)samples[i * 4 + 2];
                        result[i * 4 + 3] = (float)samples[i * 4 + 3];
                    }
                }
                break;
            case SKColorType.RgbaF32:
                {
                    var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(bytes);
                    for (int i = 0; i < count; i++)
                    {
                        result[i * 4 + 0] = samples[i * 4 + 0];
                        result[i * 4 + 1] = samples[i * 4 + 1];
                        result[i * 4 + 2] = samples[i * 4 + 2];
                        result[i * 4 + 3] = samples[i * 4 + 3];
                    }
                }
                break;
            default:
                throw new NotSupportedException($"Unsupported color type: {bitmap.ColorType}");
        }
        return result;
    }

    /// <summary>
    /// 从 RGBA float 数组创建 scRGB 浮点位图（上游的 R16G16B16A16Float 目标图）。
    /// </summary>
    public static SKBitmap FromFloatPixels(float[] pixels, int width, int height)
    {
        var bitmap = Create(width, height, SKColorType.RgbaF32);
        var span = bitmap.GetPixelSpan();
        var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(span);
        pixels.AsSpan(0, width * height * 4).CopyTo(samples);
        return bitmap;
    }

    /// <summary>
    /// 把 RGBA float 数组写成 16bit 归一化位图（上游 R16G16B16A16UIntNormalized）。
    /// </summary>
    public static SKBitmap FromNormalizedFloatPixels(float[] pixels, int width, int height)
    {
        var bitmap = Create(width, height, SKColorType.Rgba16161616);
        var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ushort>(bitmap.GetPixelSpan());
        for (int i = 0; i < pixels.Length; i++)
        {
            samples[i] = (ushort)Math.Clamp((int)MathF.Round(pixels[i] * 65535f), 0, 65535);
        }
        return bitmap;
    }
}
