using SkiaSharp;

namespace LunarWard.Codec;

/// <summary>
/// 缓冲区精度。对应 <c>Windows.Graphics.Canvas.CanvasBufferPrecision</c> 中上游用到的成员。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：Win2D 枚举在 Linux 上不存在，
/// 这里定义同名枚举保留效果类属性形态；CPU 内核固定用 32bit float 中间量。
/// </remarks>
public enum CanvasBufferPrecision
{
    Precision8UIntNormalized = 0,
    Precision16UIntNormalized = 1,
    Precision16Float = 2,
    Precision32Float = 3,
}

/// <summary>
/// sRGB 传递函数（EOTF / OETF）。对应上游
/// <c>Starward/Features/Codec/SrgbGammaEffect.cs</c>。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游是 D2D1 着色器 + Win2D
/// <c>CanvasEffect</c>；本仓库改 CPU Span 内核，分段阈值（0.04045 / 0.0031308）、
/// 指数（2.4 / 1/2.4）与 1.055/0.055 常量与上游逐位一致。
/// </remarks>
public partial class SrgbGammaEffect
{
    /// <summary>
    /// 应用方向。
    /// </summary>
    public enum SrgbGammaMode
    {
        /// <summary>sRGB → 线性。</summary>
        EOTF = 0,

        /// <summary>线性 → sRGB。</summary>
        OETF = 1,
    }

    public SrgbGammaMode GammaMode { get; set; }

    /// <summary>输入位图（浮点或归一化位图均可，内部统一转 float）。</summary>
    public SKBitmap? Source { get; set; }

    public CanvasBufferPrecision? BufferPrecision { get; set; }

    /// <summary>
    /// 执行转换。上游通过 <c>CanvasDrawingSession.DrawImage(effect)</c> 隐式渲染，
    /// 这里改为显式返回结果位图（RgbaF32）。
    /// </summary>
    public SKBitmap Apply()
    {
        ArgumentNullException.ThrowIfNull(Source);
        var pixels = PixelBitmap.ToFloatPixels(Source);
        int count = Source.Width * Source.Height;
        for (int i = 0; i < count * 4; i++)
        {
            // alpha 通道不参与传递函数；上游着色器里 rgb 与 a 分开处理。
            if ((i & 3) == 3)
            {
                continue;
            }
            pixels[i] = GammaMode == SrgbGammaMode.EOTF ? Eotf(pixels[i]) : Oetf(pixels[i]);
        }
        return PixelBitmap.FromFloatPixels(pixels, Source.Width, Source.Height);
    }


    /// <summary>sRGB EOTF：编码值 → 线性。</summary>
    public static float Eotf(float value)
    {
        // isLow = step(rgb, 0.04045)
        bool isLow = value <= 0.04045f;
        float high = MathF.Pow(MathF.Abs((value + 0.055f) / 1.055f), 2.4f);
        float low = value / 12.92f;
        return isLow ? low : high;
    }

    /// <summary>sRGB OETF：线性 → 编码值。</summary>
    public static float Oetf(float value)
    {
        bool isLow = value <= 0.0031308f;
        float high = 1.055f * MathF.Pow(MathF.Abs(value), 1 / 2.4f) - 0.055f;
        float low = value * 12.92f;
        return isLow ? low : high;
    }
}
