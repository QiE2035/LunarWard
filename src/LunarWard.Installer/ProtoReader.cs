using System.Buffers.Binary;
using System.Text;

namespace LunarWard.Installer;

/// <summary>
/// 最小 protobuf wire-format 读取器。
///
/// <para>上游 <c>Sophon.proto</c> 由 <c>Google.Protobuf</c> 的工具链生成
/// （<c>GameInstall.proto</c> / <c>Sophon.proto</c> → <c>*.cs</c>）。本项目要求
/// 删除 proto 与 gRPC 依赖，但 chunk 清单与 patch 清单本身仍是 protobuf
/// 二进制，必须解析。</para>
///
/// <para>因此这里手写一个只支持 wire type 0（varint）与 2（length-delimited）
/// 的读取器 —— HoYoPlay 的清单只用到这两种，足够且无外部依赖。</para>
/// </summary>
public ref struct ProtoReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;


    public ProtoReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }


    public bool End => _position >= _buffer.Length;

    public int Position => _position;


    /// <summary>读一个 tag，返回字段号；遇到截面尾部返回 0。</summary>
    public int ReadTag(out int wireType)
    {
        wireType = 0;
        if (End)
        {
            return 0;
        }

        var tag = (int)ReadVarint();
        wireType = tag & 7;
        return tag >> 3;
    }


    public ulong ReadVarint()
    {
        ulong result = 0;
        var shift = 0;
        while (_position < _buffer.Length)
        {
            var b = _buffer[_position++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
            if (shift >= 64)
            {
                throw new InvalidDataException("Malformed varint (longer than 10 bytes).");
            }
        }

        throw new InvalidDataException("Unexpected end of protobuf message while reading varint.");
    }


    public long ReadInt64() => (long)ReadVarint();

    public int ReadInt32() => (int)ReadVarint();

    public bool ReadBool() => ReadVarint() != 0;


    /// <summary>读 length-delimited 字段，返回其字节跨度。</summary>
    public ReadOnlySpan<byte> ReadBytes()
    {
        var length = (int)ReadVarint();
        if (length < 0 || _position + length > _buffer.Length)
        {
            throw new InvalidDataException($"Malformed length-delimited field (length {length} exceeds remaining {_buffer.Length - _position} bytes).");
        }

        var slice = _buffer.Slice(_position, length);
        _position += length;
        return slice;
    }


    public string ReadString() => Encoding.UTF8.GetString(ReadBytes());


    /// <summary>
    /// 跳过未知字段，保证向前兼容（新版本清单加入字段时不至于解析失败）。
    /// </summary>
    public void SkipField(int wireType)
    {
        switch (wireType)
        {
            case 0:
                ReadVarint();
                break;
            case 1:
                // 64-bit
                if (_position + 8 > _buffer.Length)
                {
                    throw new InvalidDataException("Unexpected end of protobuf message while skipping fixed64.");
                }

                _position += 8;
                break;
            case 2:
                ReadBytes();
                break;
            case 5:
                // 32-bit
                if (_position + 4 > _buffer.Length)
                {
                    throw new InvalidDataException("Unexpected end of protobuf message while skipping fixed32.");
                }

                _position += 4;
                break;
            default:
                throw new InvalidDataException($"Unsupported protobuf wire type {wireType}.");
        }
    }
}
