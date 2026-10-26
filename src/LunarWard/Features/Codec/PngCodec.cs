using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;

namespace LunarWard.Codec;

/// <summary>
/// PNG 容器块类型（只列出上游 PNG 路径关心的块）。
/// </summary>
public enum PngChunkType
{
    IHDR,
    PLTE,
    IDAT,
    IEND,
    cICP,
    iCCP,
    sRGB,
    gAMA,
    cHRM,
    iTXt,
    tEXt,
    tRNS,
    Unknown,
}

/// <summary>
/// cICP(coding-independent code points) 块内容，对应 PNG 第三版规范的 cICP。
/// </summary>
public struct PngcICPChunk
{
    public byte ColorPrimaries { get; set; }
    public byte TransferFunction { get; set; }
    public byte MatrixCoefficients { get; set; }
    public byte FullRangeFlag { get; set; }
}

/// <summary>
/// cHRM(chromaticities) 块内容，色度坐标已归一化到 0..1。
/// </summary>
public struct PngcHRMChunk
{
    public float WhiteX { get; set; }
    public float WhiteY { get; set; }
    public float RedX { get; set; }
    public float RedY { get; set; }
    public float GreenX { get; set; }
    public float GreenY { get; set; }
    public float BlueX { get; set; }
    public float BlueY { get; set; }
}

/// <summary>
/// 一个 PNG 块。
/// </summary>
public struct PngChunk
{
    public PngChunkType Type { get; set; }

    internal byte[]? Data { get; set; }

    public PngcICPChunk GetcICPChunk()
    {
        var data = Data ?? [];
        return new PngcICPChunk
        {
            ColorPrimaries = data.Length > 0 ? data[0] : (byte)0,
            TransferFunction = data.Length > 1 ? data[1] : (byte)0,
            MatrixCoefficients = data.Length > 2 ? data[2] : (byte)0,
            FullRangeFlag = data.Length > 3 ? data[3] : (byte)0,
        };
    }

    public byte[] GetiCCPChunk(out string name)
    {
        var data = Data ?? [];
        int separator = Array.IndexOf(data, (byte)0);
        if (separator < 0)
        {
            name = string.Empty;
            return [];
        }
        name = Encoding.ASCII.GetString(data, 0, separator);
        int offset = separator + 1;
        // compression method(1) + zlib deflate
        if (data.Length <= offset + 1)
        {
            return [];
        }
        using var input = new MemoryStream(data, offset + 1, data.Length - offset - 1);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    public PngcHRMChunk GetcHRMChunk()
    {
        var data = Data ?? [];
        float Read(int index) => data.Length >= 4 * index + 4
            ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4 * index, 4)) / 100000f
            : 0;
        return new PngcHRMChunk
        {
            WhiteX = Read(0),
            WhiteY = Read(1),
            RedX = Read(2),
            RedY = Read(3),
            GreenX = Read(4),
            GreenY = Read(5),
            BlueX = Read(6),
            BlueY = Read(7),
        };
    }
}

/// <summary>
/// PNG 块读取器。对应上游 <c>Starward.Codec.PngReader</c> 的块枚举部分
/// （上游在加载/保存 PNG 时用它读写色彩相关块）。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游 <c>Starward.Codec</c> 提供
/// <c>PngReader</c>；本仓库按上游用到的 API（<c>new PngReader(stream)</c>、
/// <c>GetNextChunk()</c>、<c>PngChunk.GetcICPChunk/GetiCCPChunk/GetcHRMChunk</c>）重建，
/// 并补齐 Skia 不支持 16bit PNG 时需要的像素解码（见 <see cref="PngDecoder"/>）。
/// </remarks>
public sealed class PngReader
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly Stream _stream;

    public PngReader(Stream stream)
    {
        _stream = stream;
        Span<byte> signature = stackalloc byte[8];
        if (stream.Read(signature) != 8 || !signature.SequenceEqual(Signature))
        {
            throw new InvalidDataException("Not a PNG file.");
        }
    }

    /// <summary>
    /// 读取下一个块；文件结束时返回 <see cref="PngChunkType.IEND"/>。
    /// </summary>
    public PngChunk GetNextChunk()
    {
        Span<byte> header = stackalloc byte[8];
        if (_stream.Read(header) != 8)
        {
            return new PngChunk { Type = PngChunkType.IEND };
        }
        int length = BinaryPrimitives.ReadInt32BigEndian(header[..4]);
        string type = Encoding.ASCII.GetString(header.Slice(4, 4));
        byte[] data = new byte[length];
        _stream.ReadExactly(data);
        Span<byte> crc = stackalloc byte[4];
        _stream.ReadExactly(crc);
        return new PngChunk
        {
            Type = type switch
            {
                "IHDR" => PngChunkType.IHDR,
                "PLTE" => PngChunkType.PLTE,
                "IDAT" => PngChunkType.IDAT,
                "IEND" => PngChunkType.IEND,
                "cICP" => PngChunkType.cICP,
                "iCCP" => PngChunkType.iCCP,
                "sRGB" => PngChunkType.sRGB,
                "gAMA" => PngChunkType.gAMA,
                "cHRM" => PngChunkType.cHRM,
                "iTXt" => PngChunkType.iTXt,
                "tEXt" => PngChunkType.tEXt,
                "tRNS" => PngChunkType.tRNS,
                _ => PngChunkType.Unknown,
            },
            Data = data,
        };
    }
}


/// <summary>
/// PNG 像素解码器。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游用 Win2D
/// <c>CanvasBitmap.LoadAsync</c> 解 PNG；本机 SkiaSharp 3.119.4 不支持 16bit PNG
/// 输出（<c>SKCodec.GetPixels</c> → <c>InvalidConversion</c>），所以 PNG 像素改为
/// 纯 CPU 解码（zlib 用 <see cref="ZLibStream"/>），保证 8bit 与 16bit 都能拿到原始样本。
/// 支持 colorType 0/2/3/4/6、bitDepth 1/2/4/8/16、非隔行；隔行 PNG（Adam7）不支持。
/// </remarks>
internal static class PngDecoder
{
    public sealed record PngImageInfo(int Width, int Height, int BitDepth, int ColorType, bool IsInterlaced);

    /// <summary>
    /// 只读 IHDR（等价于上游用 BitmapDecoder 取像素尺寸的快速路径）。
    /// </summary>
    public static PngImageInfo ReadImageInfo(Stream stream)
    {
        var reader = new PngReader(stream);
        PngChunk chunk;
        do
        {
            chunk = reader.GetNextChunk();
        }
        while (chunk.Type != PngChunkType.IHDR && chunk.Type != PngChunkType.IEND);

        var data = chunk.Data ?? [];
        if (data.Length < 13)
        {
            throw new InvalidDataException("PNG without IHDR.");
        }
        return new PngImageInfo(
            BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4)),
            BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4)),
            data[8],
            data[9],
            data[12] != 0);
    }


    /// <summary>
    /// 解码 PNG 为 SKBitmap：8bit 源 → Rgba8888，16bit 源 → Rgba16161616。
    /// </summary>
    public static SKBitmap Decode(byte[] png)
    {
        using var stream = new MemoryStream(png, writable: false);
        var reader = new PngReader(stream);
        int width = 0, height = 0, bitDepth = 8, colorType = 6, interlace = 0;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var idat = new MemoryStream();

        PngChunk chunk;
        while (true)
        {
            chunk = reader.GetNextChunk();
            if (chunk.Type == PngChunkType.IEND)
            {
                break;
            }
            var data = chunk.Data ?? [];
            switch (chunk.Type)
            {
                case PngChunkType.IHDR:
                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
                    bitDepth = data[8];
                    colorType = data[9];
                    interlace = data[12];
                    break;
                case PngChunkType.PLTE:
                    palette = data;
                    break;
                case PngChunkType.tRNS:
                    transparency = data;
                    break;
                case PngChunkType.IDAT:
                    idat.Write(data);
                    break;
            }
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("PNG without IHDR.");
        }
        if (interlace != 0)
        {
            throw new NotSupportedException("Interlaced PNG is not supported on this platform (see docs/09-LINUX-DIVERGENCES.md).");
        }

        int channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new NotSupportedException($"Unsupported PNG color type: {colorType}"),
        };
        if (bitDepth is not (1 or 2 or 4 or 8 or 16))
        {
            throw new NotSupportedException($"Unsupported PNG bit depth: {bitDepth}");
        }

        int bitsPerPixel = channels * bitDepth;
        int rowBytes = (width * bitsPerPixel + 7) / 8;
        int bytesPerPixel = Math.Max(1, (bitsPerPixel + 7) / 8);
        byte[] raw = new byte[(rowBytes + 1) * height];

        idat.Position = 0;
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress))
        {
            int read = 0;
            while (read < raw.Length)
            {
                int n = zlib.Read(raw, read, raw.Length - read);
                if (n <= 0)
                {
                    break;
                }
                read += n;
            }
        }

        byte[] pixels = new byte[rowBytes * height];
        Unfilter(raw, pixels, width, height, rowBytes, bytesPerPixel);

        bool is16 = bitDepth == 16;
        var bitmap = PixelBitmap.Create(width, height, is16 ? SKColorType.Rgba16161616 : SKColorType.Rgba8888);
        if (is16)
        {
            var samples = MemoryMarshal.Cast<byte, ushort>(bitmap.GetPixelSpan());
            Fill16(pixels, samples, width, height, colorType, rowBytes, palette, transparency);
        }
        else
        {
            var dst = bitmap.GetPixelSpan();
            Fill8(pixels, dst, width, height, bitDepth, colorType, rowBytes, palette, transparency);
        }
        return bitmap;
    }


    private static void Unfilter(byte[] raw, byte[] pixels, int width, int height, int rowBytes, int bpp)
    {
        for (int y = 0; y < height; y++)
        {
            byte filter = raw[y * (rowBytes + 1)];
            var src = raw.AsSpan(y * (rowBytes + 1) + 1, rowBytes);
            var dst = pixels.AsSpan(y * rowBytes, rowBytes);
            var prior = y == 0 ? Span<byte>.Empty : pixels.AsSpan((y - 1) * rowBytes, rowBytes);
            switch (filter)
            {
                case 0:
                    src.CopyTo(dst);
                    break;
                case 1:
                    for (int x = 0; x < rowBytes; x++)
                    {
                        byte left = x >= bpp ? dst[x - bpp] : (byte)0;
                        dst[x] = (byte)(src[x] + left);
                    }
                    break;
                case 2:
                    for (int x = 0; x < rowBytes; x++)
                    {
                        dst[x] = (byte)(src[x] + (prior.IsEmpty ? 0 : prior[x]));
                    }
                    break;
                case 3:
                    for (int x = 0; x < rowBytes; x++)
                    {
                        int left = x >= bpp ? dst[x - bpp] : 0;
                        int up = prior.IsEmpty ? 0 : prior[x];
                        dst[x] = (byte)(src[x] + ((left + up) >> 1));
                    }
                    break;
                case 4:
                    for (int x = 0; x < rowBytes; x++)
                    {
                        int left = x >= bpp ? dst[x - bpp] : 0;
                        int up = prior.IsEmpty ? 0 : prior[x];
                        int upLeft = (x >= bpp && !prior.IsEmpty) ? prior[x - bpp] : 0;
                        dst[x] = (byte)(src[x] + Paeth(left, up, upLeft));
                    }
                    break;
                default:
                    throw new InvalidDataException($"Unknown PNG filter type: {filter}");
            }
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
        {
            return a;
        }
        return pb <= pc ? b : c;
    }


    private static void Fill8(byte[] src, Span<byte> dst, int width, int height, int bitDepth, int colorType, int rowBytes, byte[]? palette, byte[]? transparency)
    {
        int maxValue = (1 << bitDepth) - 1;
        for (int y = 0; y < height; y++)
        {
            var row = src.AsSpan(y * rowBytes, rowBytes);
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 4;
                byte r, g, b, a = 255;
                switch (colorType)
                {
                    case 0:
                        {
                            int v = ReadSample(row, x, bitDepth);
                            byte gray = (byte)(v * 255 / maxValue);
                            r = g = b = gray;
                            if (transparency is { Length: >= 2 } && v == BinaryPrimitives.ReadUInt16BigEndian(transparency))
                            {
                                a = 0;
                            }
                        }
                        break;
                    case 2:
                        r = row[x * 3 + 0];
                        g = row[x * 3 + 1];
                        b = row[x * 3 + 2];
                        if (transparency is { Length: >= 6 }
                            && r == transparency[1] && g == transparency[3] && b == transparency[5])
                        {
                            a = 0;
                        }
                        break;
                    case 3:
                        {
                            int v = ReadSample(row, x, bitDepth);
                            int entry = v * 3;
                            r = palette is not null && entry + 2 < palette.Length ? palette[entry + 0] : (byte)0;
                            g = palette is not null && entry + 2 < palette.Length ? palette[entry + 1] : (byte)0;
                            b = palette is not null && entry + 2 < palette.Length ? palette[entry + 2] : (byte)0;
                            if (transparency is not null && v < transparency.Length)
                            {
                                a = transparency[v];
                            }
                        }
                        break;
                    case 4:
                        r = g = b = row[x * 2 + 0];
                        a = row[x * 2 + 1];
                        break;
                    default:
                        r = row[x * 4 + 0];
                        g = row[x * 4 + 1];
                        b = row[x * 4 + 2];
                        a = row[x * 4 + 3];
                        break;
                }
                dst[index + 0] = r;
                dst[index + 1] = g;
                dst[index + 2] = b;
                dst[index + 3] = a;
            }
        }
    }

    private static void Fill16(byte[] src, Span<ushort> dst, int width, int height, int colorType, int rowBytes, byte[]? palette, byte[]? transparency)
    {
        var samples = MemoryMarshal.Cast<byte, ushort>(src.AsSpan());
        int perRow = rowBytes / 2;
        for (int y = 0; y < height; y++)
        {
            var row = samples.Slice(y * perRow, perRow);
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 4;
                ushort r, g, b, a = ushort.MaxValue;
                switch (colorType)
                {
                    case 0:
                        r = g = b = BinaryPrimitives.ReverseEndianness(row[x]);
                        break;
                    case 2:
                        r = BinaryPrimitives.ReverseEndianness(row[x * 3 + 0]);
                        g = BinaryPrimitives.ReverseEndianness(row[x * 3 + 1]);
                        b = BinaryPrimitives.ReverseEndianness(row[x * 3 + 2]);
                        break;
                    case 4:
                        r = g = b = BinaryPrimitives.ReverseEndianness(row[x * 2 + 0]);
                        a = BinaryPrimitives.ReverseEndianness(row[x * 2 + 1]);
                        break;
                    default:
                        r = BinaryPrimitives.ReverseEndianness(row[x * 4 + 0]);
                        g = BinaryPrimitives.ReverseEndianness(row[x * 4 + 1]);
                        b = BinaryPrimitives.ReverseEndianness(row[x * 4 + 2]);
                        a = BinaryPrimitives.ReverseEndianness(row[x * 4 + 3]);
                        break;
                }
                dst[index + 0] = r;
                dst[index + 1] = g;
                dst[index + 2] = b;
                dst[index + 3] = a;
            }
        }
    }

    private static int ReadSample(ReadOnlySpan<byte> row, int x, int bitDepth)
    {
        return bitDepth switch
        {
            8 => row[x],
            16 => BinaryPrimitives.ReadUInt16BigEndian(row.Slice(x * 2, 2)),
            _ => (row[(x * bitDepth) / 8] >> (8 - bitDepth - ((x * bitDepth) % 8))) & ((1 << bitDepth) - 1),
        };
    }
}


/// <summary>
/// PNG 编码器（重写色彩块 + IDAT + iTXt XMP）。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游用 Win2D <c>BitmapEncoder</c>
/// 编码后由 <c>PngReader</c> 重写色彩块；本仓库改为一次性写出完整 PNG
/// （IHDR + 色彩块 + iTXt + IDAT + IEND，自算 CRC32），避开 Skia 不支持
/// Rgba16161616 编码的限制（实测 <c>SKImage.Encode(Png)</c> 返回 0 字节）。
/// 写出规则与上游一致：BT.2020 → cICP(9, 16, 0, 1)；BT.709 → sRGB；
/// 自定义原色 → cHRM；有 ICC 时写 iCCP("ICC Profile")。
/// </remarks>
internal static class PngEncoder
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    /// 写出 PNG。
    /// </summary>
    /// <param name="bitmap">Rgba8888 或 Rgba16161616 位图。</param>
    /// <param name="writeColorProfile">是否写色彩块（对应上游同名参数）。</param>
    public static void Write(Stream stream, SKBitmap bitmap, ICC.ColorPrimaries colorPrimaries, byte[]? iccData, byte[]? xmpData, bool writeColorProfile)
    {
        bool is16 = bitmap.ColorType == SKColorType.Rgba16161616;
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], bitmap.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), bitmap.Height);
        ihdr[8] = (byte)(is16 ? 16 : 8);
        ihdr[9] = 6; // truecolor with alpha
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;

        WriteSignature(stream);
        WriteChunk(stream, "IHDR", ihdr);

        if (writeColorProfile)
        {
            if (colorPrimaries.Id == 9)
            {
                WriteChunk(stream, "cICP", [9, 16, 0, 1]);
            }
            else if (iccData is { Length: > 0 })
            {
                WriteiCCP(stream, iccData);
            }
            else if (colorPrimaries.Id == 1)
            {
                WriteChunk(stream, "sRGB", [0]);
            }
            else if (colorPrimaries.IsValid)
            {
                WritecHRM(stream, colorPrimaries);
            }
        }

        if (xmpData is { Length: > 0 })
        {
            WriteiTXt(stream, "XML:com.adobe.xmp", xmpData);
        }

        WriteChunk(stream, "IDAT", CompressPixels(bitmap, is16));
        WriteChunk(stream, "IEND", []);
    }


    private static byte[] CompressPixels(SKBitmap bitmap, bool is16)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        int rowBytes = width * (is16 ? 8 : 4);
        byte[] raw = new byte[(rowBytes + 1) * height];
        var source = bitmap.GetPixelSpan();
        for (int y = 0; y < height; y++)
        {
            raw[y * (rowBytes + 1)] = 0; // filter: None
            var src = source.Slice(y * rowBytes, rowBytes);
            var dst = raw.AsSpan(y * (rowBytes + 1) + 1, rowBytes);
            if (is16)
            {
                // PNG 16bit 样本为大端
                for (int i = 0; i < rowBytes; i += 2)
                {
                    dst[i] = src[i + 1];
                    dst[i + 1] = src[i];
                }
            }
            else
            {
                src.CopyTo(dst);
            }
        }

        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }
        return output.ToArray();
    }


    private static void WriteSignature(Stream stream)
    {
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        Encoding.ASCII.GetBytes(type, typeBytes);
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = 0xFFFFFFFF;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFF);
        stream.Write(crcBytes);
    }

    private static void WriteiCCP(Stream stream, byte[] iccData)
    {
        using var output = new MemoryStream();
        var name = Encoding.ASCII.GetBytes("ICC Profile");
        output.Write(name);
        output.WriteByte(0); // separator
        output.WriteByte(0); // compression method: deflate
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(iccData);
        }
        WriteChunk(stream, "iCCP", output.ToArray());
    }

    private static void WritecHRM(Stream stream, ICC.ColorPrimaries primaries)
    {
        Span<byte> data = stackalloc byte[32];
        // 上游无此函数（Win2D 直接写 cHRM）；本地 Span 不能进 lambda 捕获，改为显式传参。
        static void W(Span<byte> target, int i, float value) => BinaryPrimitives.WriteUInt32BigEndian(target.Slice(i * 4, 4), (uint)Math.Round(value * 100000f));
        W(data, 0, primaries.White.X);
        W(data, 1, primaries.White.Y);
        W(data, 2, primaries.Red.X);
        W(data, 3, primaries.Red.Y);
        W(data, 4, primaries.Green.X);
        W(data, 5, primaries.Green.Y);
        W(data, 6, primaries.Blue.X);
        W(data, 7, primaries.Blue.Y);
        WriteChunk(stream, "cHRM", data);
    }

    private static void WriteiTXt(Stream stream, string keyword, byte[] text)
    {
        using var output = new MemoryStream();
        output.Write(Encoding.ASCII.GetBytes(keyword));
        output.WriteByte(0);
        output.WriteByte(0); // compression flag: uncompressed
        output.WriteByte(0); // compression method
        output.WriteByte(0); // language tag (empty)
        output.WriteByte(0); // translated keyword (empty)
        output.Write(text);
        WriteChunk(stream, "iTXt", output.ToArray());
    }


    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc;
    }
}
