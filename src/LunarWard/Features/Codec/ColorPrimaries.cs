using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace LunarWard.Codec.ICC;

/// <summary>
/// 色彩原色（chromaticity + 标准编号）。对应上游 <c>Starward.Codec.ICC.ColorPrimaries</c>。
/// </summary>
/// <remarks>
/// 上游该类型来自 <c>Starward.Codec</c> NuGet 包（源码不在仓库），本仓库按调用面重建，
/// 成员、静态实例与 <see cref="GetColorTransferMatrix"/> 的返回类型
/// （<see cref="Matrix4x4"/>，Screenshot 侧 <c>ToMatrix5x4</c> 直接取 M11..M44）保持一致。
/// </remarks>
public sealed class ColorPrimaries
{
    /// <summary>
    /// CICP / H.273 colour primaries 编号。BT.709 = 1，BT.2020 = 9，Display P3 = 12，
    /// 自定义（cHRM / ICC 推导）= 0。
    /// </summary>
    public int Id { get; init; }

    public Vector2 Red { get; init; }
    public Vector2 Green { get; init; }
    public Vector2 Blue { get; init; }
    public Vector2 White { get; init; }

    /// <summary>
    /// 色度坐标是否可用（不依赖 <see cref="Id"/>：cHRM 自定义原色的 Id 为 0，
    /// 但仍应保留，不能在上游的 BT.709 兜底逻辑里被覆盖）。
    /// </summary>
    public bool IsValid
        => Red.Y > 0 && Green.Y > 0 && Blue.Y > 0 && White.Y > 0
        && float.IsFinite(Red.X) && float.IsFinite(Green.X) && float.IsFinite(Blue.X);

    public override string ToString() => $"ColorPrimaries({Id}: R{Red} G{Green} B{Blue} W{White})";


    public static ColorPrimaries BT709 { get; } = new()
    {
        Id = 1,
        Red = new Vector2(0.640f, 0.330f),
        Green = new Vector2(0.300f, 0.600f),
        Blue = new Vector2(0.150f, 0.060f),
        White = new Vector2(0.3127f, 0.3290f),
    };

    public static ColorPrimaries DisplayP3 { get; } = new()
    {
        Id = 12,
        Red = new Vector2(0.680f, 0.320f),
        Green = new Vector2(0.265f, 0.690f),
        Blue = new Vector2(0.150f, 0.060f),
        White = new Vector2(0.3127f, 0.3290f),
    };

    public static ColorPrimaries BT2020 { get; } = new()
    {
        Id = 9,
        Red = new Vector2(0.708f, 0.292f),
        Green = new Vector2(0.170f, 0.797f),
        Blue = new Vector2(0.131f, 0.046f),
        White = new Vector2(0.3127f, 0.3290f),
    };

    public static ColorPrimaries Unspecified { get; } = new()
    {
        Id = 2,
    };


    /// <summary>
    /// 从色度坐标构造，并按已知标准归类 <see cref="Id"/>。
    /// </summary>
    public static ColorPrimaries FromChromaticities(Vector2 red, Vector2 green, Vector2 blue, Vector2 white)
    {
        var primaries = new ColorPrimaries
        {
            Red = red,
            Green = green,
            Blue = blue,
            White = white,
        };
        return WithClassifiedId(primaries);
    }

    /// <summary>
    /// 按色度坐标匹配 CICP 已知原色（容差 0.01），匹配不上返回 Id = 0 的自定义原色。
    /// </summary>
    public static ColorPrimaries WithClassifiedId(ColorPrimaries primaries)
    {
        if (Matches(primaries, BT2020))
        {
            return new ColorPrimaries { Id = 9, Red = primaries.Red, Green = primaries.Green, Blue = primaries.Blue, White = primaries.White };
        }
        if (Matches(primaries, DisplayP3))
        {
            return new ColorPrimaries { Id = 12, Red = primaries.Red, Green = primaries.Green, Blue = primaries.Blue, White = primaries.White };
        }
        if (Matches(primaries, BT709))
        {
            return new ColorPrimaries { Id = 1, Red = primaries.Red, Green = primaries.Green, Blue = primaries.Blue, White = primaries.White };
        }
        return new ColorPrimaries { Id = 0, Red = primaries.Red, Green = primaries.Green, Blue = primaries.Blue, White = primaries.White };
    }

    private static bool Matches(ColorPrimaries a, ColorPrimaries b)
        => Vector2.Distance(a.Red, b.Red) < 0.01f
        && Vector2.Distance(a.Green, b.Green) < 0.01f
        && Vector2.Distance(a.Blue, b.Blue) < 0.01f
        && Vector2.Distance(a.White, b.White) < 0.01f;


    /// <summary>
    /// 求从 <paramref name="source"/> 线性 RGB 到 <paramref name="target"/> 线性 RGB 的 3x3 矩阵，
    /// 以 <see cref="Matrix4x4"/> 返回（行主序、按 D2D ColorMatrixEffect 的转置布局：
    /// <c>out.X = M11*r + M21*g + M31*b</c>），与上游 <c>Starward.Codec</c> 的返回语义一致。
    /// </summary>
    public static Matrix4x4 GetColorTransferMatrix(ColorPrimaries source, ColorPrimaries target)
    {
        var sourceToXyz = GetRgbToXyzMatrix(source);
        var targetToXyz = GetRgbToXyzMatrix(target);
        var targetFromXyz = Invert3x3(targetToXyz);
        var standard = Multiply3x3(targetFromXyz, sourceToXyz);
        return new Matrix4x4(
            (float)standard[0, 0], (float)standard[1, 0], (float)standard[2, 0], 0,
            (float)standard[0, 1], (float)standard[1, 1], (float)standard[2, 1], 0,
            (float)standard[0, 2], (float)standard[1, 2], (float)standard[2, 2], 0,
            0, 0, 0, 1);
    }


    /// <summary>
    /// 计算 RGB（Y 归一化到 1）到 XYZ 的矩阵，行主序：[row=output XYZ, col=input RGB]。
    /// </summary>
    internal static double[,] GetRgbToXyzMatrix(ColorPrimaries primaries)
    {
        double xr = primaries.Red.X, yr = primaries.Red.Y;
        double xg = primaries.Green.X, yg = primaries.Green.Y;
        double xb = primaries.Blue.X, yb = primaries.Blue.Y;
        double xw = primaries.White.X, yw = primaries.White.Y;

        double[,] p = new double[3, 3]
        {
            { xr / yr, xg / yg, xb / yb },
            { 1, 1, 1 },
            { (1 - xr - yr) / yr, (1 - xg - yg) / yg, (1 - xb - yb) / yb },
        };
        double[] white = [xw / yw, 1, (1 - xw - yw) / yw];
        double[,] pInv = Invert3x3(p);
        double[] s = Multiply3x3(pInv, white);
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                p[row, col] *= s[col];
            }
        }
        return p;
    }


    internal static double[] Multiply3x3(double[,] m, double[] v)
    {
        double[] result = new double[3];
        for (int row = 0; row < 3; row++)
        {
            result[row] = m[row, 0] * v[0] + m[row, 1] * v[1] + m[row, 2] * v[2];
        }
        return result;
    }

    internal static double[,] Multiply3x3(double[,] a, double[,] b)
    {
        double[,] result = new double[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                result[row, col] = a[row, 0] * b[0, col] + a[row, 1] * b[1, col] + a[row, 2] * b[2, col];
            }
        }
        return result;
    }

    internal static double[,] Invert3x3(double[,] m)
    {
        double a = m[0, 0], b = m[0, 1], c = m[0, 2];
        double d = m[1, 0], e = m[1, 1], f = m[1, 2];
        double g = m[2, 0], h = m[2, 1], i = m[2, 2];
        double det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
        double inv = 1 / det;
        return new double[3, 3]
        {
            { (e * i - f * h) * inv, (c * h - b * i) * inv, (b * f - c * e) * inv },
            { (f * g - d * i) * inv, (a * i - c * g) * inv, (c * d - a * f) * inv },
            { (d * h - e * g) * inv, (b * g - a * h) * inv, (a * e - b * d) * inv },
        };
    }
}


/// <summary>
/// ICC 配置文件解析与生成。对应上游 <c>Starward.Codec.ICC.ICCHelper</c>。
/// </summary>
/// <remarks>
/// 只实现上游调用到的能力：从 RGB matrix/TRC profile 读 rXYZ/gXYZ/bXYZ/wtpt 推导原色；
/// 按原色生成一个可被 libpng/avifenc/浏览器识别的 D50 matrix+TRC profile。
/// 不做完整的 v4 lut 支持（登记于 <c>docs/09-LINUX-DIVERGENCES.md</c>）。
/// </remarks>
public static class ICCHelper
{
    private const double D50X = 0.9642;
    private const double D50Y = 1.0000;
    private const double D50Z = 0.8249;

    /// <summary>
    /// 从 ICC 配置文件数据推导色彩原色。无法解析时返回 <c>null</c>。
    /// </summary>
    public static ColorPrimaries? GetColorPrimariesFromIccData(ReadOnlySpan<byte> iccData)
    {
        try
        {
            if (iccData.Length < 132)
            {
                return null;
            }
            // profile header: size(4) cmm(4) version(4) class(4) colorspace(4) pcs(4) ...
            if (!iccData.Slice(36, 4).SequenceEqual("acsp"u8))
            {
                return null;
            }
            if (!iccData.Slice(16, 4).SequenceEqual("RGB "u8))
            {
                return null;
            }

            int tagCount = (int)BinaryPrimitives.ReadUInt32BigEndian(iccData.Slice(128, 4));
            if (tagCount <= 0 || 132 + tagCount * 12 > iccData.Length)
            {
                return null;
            }

            Vector3? r = null, g = null, b = null, w = null;
            for (int i = 0; i < tagCount; i++)
            {
                var entry = iccData.Slice(132 + i * 12, 12);
                var signature = Encoding.ASCII.GetString(entry[..4]);
                int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(entry.Slice(4, 4));
                int size = (int)BinaryPrimitives.ReadUInt32BigEndian(entry.Slice(8, 4));
                if (offset <= 0 || size < 20 || offset + size > iccData.Length)
                {
                    continue;
                }
                var tagData = iccData.Slice(offset, size);
                if (!tagData[..4].SequenceEqual("XYZ "u8))
                {
                    continue;
                }
                var xyz = ReadXyz(tagData);
                switch (signature)
                {
                    case "rXYZ": r = xyz; break;
                    case "gXYZ": g = xyz; break;
                    case "bXYZ": b = xyz; break;
                    case "wtpt": w = xyz; break;
                }
            }

            if (r is null || g is null || b is null)
            {
                return null;
            }

            // profile 的 XYZ 是 PCS（D50 已适配）值，直接换算色度坐标即可反推原色。
            var red = XyzToChromacity(r.Value);
            var green = XyzToChromacity(g.Value);
            var blue = XyzToChromacity(b.Value);
            var white = XyzToChromacity(w ?? new Vector3((float)D50X, (float)D50Y, (float)D50Z));
            return ColorPrimaries.FromChromaticities(red, green, blue, white);
        }
        catch
        {
            return null;
        }
    }


    private static Vector3 ReadXyz(ReadOnlySpan<byte> tagData)
    {
        return new Vector3(
            ReadS15Fixed16(tagData.Slice(8, 4)),
            ReadS15Fixed16(tagData.Slice(12, 4)),
            ReadS15Fixed16(tagData.Slice(16, 4)));
    }

    private static float ReadS15Fixed16(ReadOnlySpan<byte> data)
        => BinaryPrimitives.ReadInt32BigEndian(data) / 65536f;

    private static Vector2 XyzToChromacity(Vector3 xyz)
    {
        float sum = xyz.X + xyz.Y + xyz.Z;
        if (sum == 0)
        {
            return Vector2.Zero;
        }
        return new Vector2(xyz.X / sum, xyz.Y / sum);
    }


    /// <summary>
    /// 按原色生成 ICC（v2 matrix/TRC，PCS 为 D50，白点经 Bradford 适配）。
    /// 上游同名方法生成的是 Windows 色彩管理同样能识别的等价 profile；
    /// 这里用最小可用实现，TRC 取 gamma 2.2（上游未公开其 TRC 具体曲线）。
    /// </summary>
    public static byte[] CreateIccData(ColorPrimaries primaries)
    {
        var rgbToXyz = AdaptToD50(primitivesToXyz: ColorPrimaries.GetRgbToXyzMatrix(primaries), white: primaries.White);

        byte[] desc = BuildTextDescription("LunarWard Color Primaries");
        byte[] cprt = BuildText("No copyright, use freely");
        byte[] wtpt = BuildXyz((float)D50X, (float)D50Y, (float)D50Z);
        byte[] rXyz = BuildXyz((float)rgbToXyz[0, 0], (float)rgbToXyz[1, 0], (float)rgbToXyz[2, 0]);
        byte[] gXyz = BuildXyz((float)rgbToXyz[0, 1], (float)rgbToXyz[1, 1], (float)rgbToXyz[2, 1]);
        byte[] bXyz = BuildXyz((float)rgbToXyz[0, 2], (float)rgbToXyz[1, 2], (float)rgbToXyz[2, 2]);
        byte[] trc = BuildGammaCurve(2.2f);

        (string Signature, byte[] Data)[] tags =
        [
            ("desc", desc),
            ("cprt", cprt),
            ("wtpt", wtpt),
            ("rXYZ", rXyz),
            ("gXYZ", gXyz),
            ("bXYZ", bXyz),
            ("rTRC", trc),
            ("gTRC", trc),
            ("bTRC", trc),
        ];

        int tagTableSize = 4 + tags.Length * 12;
        int dataSize = tags.Sum(t => Align4(t.Data.Length));
        int totalSize = 128 + tagTableSize + dataSize;

        byte[] icc = new byte[totalSize];
        var span = icc.AsSpan();

        BinaryPrimitives.WriteUInt32BigEndian(span[0..4], (uint)totalSize);
        "LunarWard"u8.CopyTo(span[4..12]);
        BinaryPrimitives.WriteUInt32BigEndian(span[8..12], 0x02100000);
        "mntr"u8.CopyTo(span[12..16]);
        "RGB "u8.CopyTo(span[16..20]);
        "XYZ "u8.CopyTo(span[20..24]);
        BinaryPrimitives.WriteUInt16BigEndian(span[24..26], 2026);
        BinaryPrimitives.WriteUInt16BigEndian(span[26..28], 1);
        BinaryPrimitives.WriteUInt16BigEndian(span[28..30], 1);
        BinaryPrimitives.WriteUInt16BigEndian(span[30..32], 0);
        BinaryPrimitives.WriteUInt16BigEndian(span[32..34], 0);
        BinaryPrimitives.WriteUInt16BigEndian(span[34..36], 0);
        "acsp"u8.CopyTo(span[36..40]);
        // rendering intent 0, illuminant D50 (s15Fixed16)
        BinaryPrimitives.WriteInt32BigEndian(span[68..72], (int)Math.Round(D50X * 65536));
        BinaryPrimitives.WriteInt32BigEndian(span[72..76], (int)Math.Round(D50Y * 65536));
        BinaryPrimitives.WriteInt32BigEndian(span[76..80], (int)Math.Round(D50Z * 65536));

        BinaryPrimitives.WriteUInt32BigEndian(span[128..132], (uint)tags.Length);
        int offset = 128 + tagTableSize;
        for (int i = 0; i < tags.Length; i++)
        {
            var entry = span.Slice(132 + i * 12, 12);
            Encoding.ASCII.GetBytes(tags[i].Signature).CopyTo(entry);
            BinaryPrimitives.WriteUInt32BigEndian(entry[4..8], (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(entry[8..12], (uint)tags[i].Data.Length);
            tags[i].Data.CopyTo(span[offset..]);
            offset += Align4(tags[i].Data.Length);
        }

        return icc;
    }


    private static int Align4(int value) => (value + 3) & ~3;

    private static double[,] AdaptToD50(double[,] primitivesToXyz, Vector2 white)
    {
        // Bradford 色适应：D65 白点 → D50
        double[,] bradford =
        {
            { 0.8951, 0.2664, -0.1614 },
            { -0.7502, 1.7135, 0.0367 },
            { 0.0389, -0.0685, 1.0296 },
        };
        double[] srcWhite = ColorPrimaries.Multiply3x3(bradford, [white.X / white.Y, 1, (1 - white.X - white.Y) / white.Y]);
        double[] dstWhite = ColorPrimaries.Multiply3x3(bradford, [D50X, D50Y, D50Z]);
        double[,] scale = new double[3, 3];
        for (int i = 0; i < 3; i++)
        {
            scale[i, i] = srcWhite[i] == 0 ? 1 : dstWhite[i] / srcWhite[i];
        }
        var adaptation = ColorPrimaries.Multiply3x3(ColorPrimaries.Invert3x3(bradford), ColorPrimaries.Multiply3x3(scale, bradford));
        return ColorPrimaries.Multiply3x3(adaptation, primitivesToXyz);
    }

    private static byte[] BuildXyz(float x, float y, float z)
    {
        byte[] data = new byte[20];
        "XYZ "u8.CopyTo(data);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8), (int)Math.Round(x * 65536));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(12), (int)Math.Round(y * 65536));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(16), (int)Math.Round(z * 65536));
        return data;
    }

    private static byte[] BuildGammaCurve(float gamma)
    {
        byte[] data = new byte[16];
        "curv"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(12), (ushort)Math.Round(gamma * 256));
        return data;
    }

    private static byte[] BuildText(string text)
    {
        byte[] body = Encoding.ASCII.GetBytes(text + "\0");
        byte[] data = new byte[8 + body.Length];
        "text"u8.CopyTo(data);
        body.CopyTo(data.AsSpan(8));
        return data;
    }

    private static byte[] BuildTextDescription(string text)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(text + "\0");
        byte[] data = new byte[8 + 4 + 4 + ascii.Length + 4 + 2 + 1 + 67];
        "desc"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)ascii.Length);
        ascii.CopyTo(data.AsSpan(12));
        return data;
    }
}
