using System.Buffers;
using System.Diagnostics;

namespace LunarWard.Codec;

/// <summary>
/// VP9/Vorbis 解码器辅助。对应上游 <c>Starward/Features/Codec/VP9Helper.cs</c>。
/// </summary>
/// <remarks>
/// 平台替换（逐条登记于 <c>docs/09-LINUX-DIVERGENCES.md</c>）：
/// <list type="bullet">
/// <item>上游通过 Media Foundation Transform（<c>Starward.Codec.VP9Decoder.RegisterVP9DecoderLocal</c>）
/// 动态注册 VP9/Vorbis 解码器；Linux 没有 MFT，解码由 ffmpeg/libdav1d/libvpx 内置提供，
/// 因此 <see cref="RegisterVP9Decoder(bool)"/> 等方法只维护与上游同名的状态位，
/// 不注册任何系统组件（不是空实现：状态位的语义与上游一致，实际能力由系统解码器提供）。</item>
/// <item><see cref="IsVP9DecoderInstalled"/> 上游查注册表 <c>Microsoft.VP9VideoExtensions</c> 包；
/// 本仓库改为探测 PATH 中的 <c>ffmpeg</c>（存在即具备 VP9 解码能力）。</item>
/// <item><see cref="IsVP9HighProfileOrRGB"/> / <see cref="IsVP8VideoFile"/> 是纯托管的 EBML/VP9 帧头解析，
/// 与上游逐字一致，无平台差异。</item>
/// </list>
/// </remarks>
public partial class VP9Helper
{


    public static bool VP9MFTRegistered { get; private set; }

    public static bool VorbisMFTRegistered { get; private set; }

    private static bool _videoBackground;


    /// <summary>
    /// 标记 VP9 解码器可用（上游是注册 MFT；Linux 由系统解码器提供，这里只维护状态位）。
    /// </summary>
    public static void RegisterVP9Decoder(bool videoBackground = false)
    {
        try
        {
            VP9MFTRegistered = true;
            if (videoBackground)
            {
                _videoBackground = true;
            }
        }
        catch { }
    }


    /// <summary>
    /// 清除 VP9 解码器状态位（上游是注销 MFT）。
    /// </summary>
    public static void UnregisterVP9Decoder(bool videoBackground = false)
    {
        try
        {
            if (_videoBackground && !videoBackground)
            {
                return;
            }
            VP9MFTRegistered = false;
            _videoBackground = false;
        }
        catch { }
    }


    /// <summary>
    /// 标记 Vorbis 解码器可用（上游是注册 MFT；Linux 由系统解码器提供，这里只维护状态位）。
    /// </summary>
    public static void RegisterVorbisDecoder()
    {
        try
        {
            VorbisMFTRegistered = true;
        }
        catch { }
    }


    /// <summary>
    /// 清除 Vorbis 解码器状态位（上游是注销 MFT）。
    /// </summary>
    public static void UnregisterVorbisDecoder()
    {
        try
        {
            VorbisMFTRegistered = false;
        }
        catch { }
    }


    /// <summary>
    /// 是否具备 VP9 解码能力。
    /// </summary>
    /// <remarks>上游查注册表里的 <c>Microsoft.VP9VideoExtensions</c> 包；本仓库探测 PATH 中的 ffmpeg。</remarks>
    public static bool IsVP9DecoderInstalled()
    {
        try
        {
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    if (File.Exists(Path.Combine(directory, "ffmpeg")))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }



    /// <summary>
    /// 判断给定的文件是否是 VP9 编码的高 Profile 或 RGB 像素格式视频文件。
    /// </summary>
    public static bool IsVP9HighProfileOrRGB(string filePath)
    {
        const int ReadSize = 1 << 20;
        byte[]? rented = null;
        try
        {
            using var fs = File.OpenRead(filePath);
            rented = ArrayPool<byte>.Shared.Rent(ReadSize);
            int read = fs.Read(rented, 0, ReadSize);
            ReadOnlySpan<byte> buffer = rented.AsSpan(0, read);

            if (!IsWebMFile(buffer))
            {
                return false;
            }

            // 查找 SimpleBlock (0xA3) 或 Block (0xA1)
            while (buffer.Length > 0)
            {
                int index = buffer.IndexOfAny((byte)0xA1, (byte)0xA3);
                if (index < 0)
                {
                    break;
                }
                buffer = buffer.Slice(index + 1);
                // 跳过 block size (EBML VINT)
                if (!SkipEBMLVInt(buffer, out int offset))
                {
                    continue;
                }
                buffer = buffer.Slice(offset);
                // 跳过 track number (EBML VINT)
                if (!SkipEBMLVInt(buffer, out offset))
                {
                    continue;
                }
                buffer = buffer.Slice(offset);
                // 跳过 timecode (2 bytes) 和 flags (1 byte)
                if (buffer.Length < 3)
                {
                    continue;
                }
                buffer = buffer.Slice(3);

                if (buffer.Length > 0)
                {
                    var flags = ParseVP9ProfileFlags(buffer);
                    if (flags.IsValid)
                    {
                        return flags.IsHighProfile || flags.IsRgb;
                    }
                    // profile == -1 表示无法从当前字节解析帧头，继续查找下一个块
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }


    /// <summary>
    /// 判断给定的文件是否是 VP8 编码的 WebM 视频文件。
    /// </summary>
    public static bool IsVP8VideoFile(string filePath)
    {
        const int ReadSize = 1 << 20;
        byte[]? rented = null;
        try
        {
            using var fs = File.OpenRead(filePath);
            rented = ArrayPool<byte>.Shared.Rent(ReadSize);
            int read = fs.Read(rented, 0, ReadSize);
            ReadOnlySpan<byte> buffer = rented.AsSpan(0, read);

            if (!IsWebMFile(buffer))
            {
                return false;
            }

            // 在 Tracks 段中查找 CodecID 元素 (0x86) 包含 "V_VP8" 的记录
            return buffer.IndexOf("V_VP8"u8) >= 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }


    private static bool IsWebMFile(ReadOnlySpan<byte> buffer)
    {
        // EBML magic: 0x1A 0x45 0xDF 0xA3
        ReadOnlySpan<byte> ebmlMagic = [0x1A, 0x45, 0xDF, 0xA3];
        if (!buffer.StartsWith(ebmlMagic))
        {
            return false;
        }
        // DocType element: ID=0x4282, Size=0x84 (4 bytes), Value="webm"
        ReadOnlySpan<byte> docTypePrefix = [0x42, 0x82, 0x84];
        int index = buffer.IndexOf(docTypePrefix);
        if (index < 0 || index + docTypePrefix.Length + 4 > buffer.Length)
        {
            return false;
        }
        return buffer.Slice(index + docTypePrefix.Length, 4).SequenceEqual("webm"u8);
    }


    /// <summary>
    /// 从 VP9 帧头解析是否为高 Profile 或 RGB 像素格式。
    /// </summary>
    private static VP9ProfileFlags ParseVP9ProfileFlags(ReadOnlySpan<byte> buffer)
    {
        var reader = new BitReader(buffer);

        // frame_marker 占最高 2 位，固定为 0b10
        if (!reader.TryReadBits(2, out int frameMarker) || frameMarker != 2)
        {
            return default;
        }

        if (!reader.TryReadBits(2, out int profileBits))
        {
            return default;
        }

        int profile = ((profileBits & 0x01) << 1) | ((profileBits >> 1) & 0x01);
        if (profile == 3 && !reader.TryReadBit(out _))
        {
            return default;
        }

        if (!reader.TryReadBit(out bool showExistingFrame))
        {
            return default;
        }
        if (showExistingFrame)
        {
            return new VP9ProfileFlags(true, profile != 0, false);
        }

        if (!reader.TryReadBit(out bool keyFrame)
            || !reader.TryReadBit(out bool showFrame)
            || !reader.TryReadBit(out bool errorResilientMode))
        {
            return default;
        }

        bool intraOnly = false;
        if (!keyFrame)
        {
            if (!showFrame)
            {
                if (!reader.TryReadBit(out intraOnly))
                {
                    return default;
                }
            }

            if (!errorResilientMode)
            {
                if (!reader.TryReadBits(2, out _))
                {
                    return default;
                }
            }
        }

        bool hasColorConfig = keyFrame || intraOnly;
        if (!hasColorConfig)
        {
            return new VP9ProfileFlags(true, profile != 0, false);
        }

        if (!reader.TryReadBits(24, out int syncCode) || syncCode != 0x498342)
        {
            return default;
        }

        if (profile >= 2 && !reader.TryReadBit(out _))
        {
            return default;
        }

        if (!reader.TryReadBits(3, out int colorSpace))
        {
            return default;
        }

        bool isRgb = false;
        if (colorSpace == 7)
        {
            if (profile is not (1 or 3))
            {
                return default;
            }
            if (!reader.TryReadBit(out _)
                || !reader.TryReadBit(out _))
            {
                return default;
            }
            isRgb = true;
        }

        return new VP9ProfileFlags(true, profile != 0, isRgb);
    }


    /// <summary>
    /// 跳过一个 EBML VINT，返回其占用的字节数。
    /// </summary>
    private static bool SkipEBMLVInt(ReadOnlySpan<byte> buffer, out int offset)
    {
        offset = 0;
        if (buffer.Length == 0)
        {
            return false;
        }
        byte firstByte = buffer[0];
        for (int i = 0; i < 8; i++)
        {
            if ((firstByte & (0x80 >> i)) != 0)
            {
                offset = i + 1;
                return offset <= buffer.Length;
            }
        }
        return false;
    }


    private readonly record struct VP9ProfileFlags(bool IsValid, bool IsHighProfile, bool IsRgb);


    private ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _buffer;
        private int _bitOffset;

        public BitReader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _bitOffset = 0;
        }

        public bool TryReadBit(out bool value)
        {
            if (!TryReadBits(1, out int bits))
            {
                value = false;
                return false;
            }
            value = bits != 0;
            return true;
        }

        public bool TryReadBits(int bitCount, out int value)
        {
            value = 0;
            if (bitCount <= 0)
            {
                return true;
            }

            for (int i = 0; i < bitCount; i++)
            {
                int byteOffset = _bitOffset >> 3;
                if (byteOffset >= _buffer.Length)
                {
                    value = 0;
                    return false;
                }

                int bitInByte = 7 - (_bitOffset & 0x07);
                int bit = (_buffer[byteOffset] >> bitInByte) & 0x01;
                value = (value << 1) | bit;
                _bitOffset++;
            }
            return true;
        }
    }


}
