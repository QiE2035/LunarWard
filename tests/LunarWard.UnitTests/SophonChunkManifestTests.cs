using System.Text;
using LunarWard.Installer;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>手写 protobuf 编码，喂给 <c>SophonChunkManifest.Parse</c>（对应上游 Sophon.proto）。</summary>
public sealed class SophonChunkManifestTests
{
    private static byte[] Varint(long value)
    {
        var bytes = new List<byte>();
        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }

        bytes.Add((byte)value);
        return [.. bytes];
    }

    private static byte[] Tag(int field, int wire) => Varint((field << 3) | wire);

    private static byte[] VarintField(int field, long value) => [.. Tag(field, 0), .. Varint(value)];

    private static byte[] BytesField(int field, byte[] payload) => [.. Tag(field, 2), .. Varint(payload.Length), .. payload];

    private static byte[] UTF8(int field, string text) => BytesField(field, Encoding.UTF8.GetBytes(text));

    /// <summary>构造一个「1 个文件 + 1 个 chunk」的最小合法清单。</summary>
    private static byte[] BuildManifest()
    {
        byte[] chunk = [.. UTF8(1, "c0ffee"), .. VarintField(3, 0), .. VarintField(4, 5), .. VarintField(5, 9), .. UTF8(7, "compressed-md5")];
        byte[] file = [.. UTF8(1, "Game/Data/a.pak"), .. BytesField(2, chunk), .. VarintField(4, 14), .. UTF8(5, "file-md5")];
        return BytesField(1, file);
    }

    [Fact]
    public void Parse_ReadsFileAndChunk()
    {
        var manifest = SophonChunkManifest.Parse(BuildManifest());

        var file = Assert.Single(manifest.Chunks);
        Assert.Equal("Game/Data/a.pak", file.File);
        Assert.Equal(14, file.Size);
        Assert.Equal("file-md5", file.Md5);
        Assert.False(file.IsFolder);

        var chunk = Assert.Single(file.Chunks);
        Assert.Equal("c0ffee", chunk.Id);
        Assert.Equal(0, chunk.Offset);
        Assert.Equal(5, chunk.CompressedSize);
        Assert.Equal(9, chunk.UncompressedSize);
        Assert.Equal("compressed-md5", chunk.CompressedMd5);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyManifest()
    {
        var manifest = SophonChunkManifest.Parse([]);
        Assert.Empty(manifest.Chunks);
    }

    [Fact]
    public void Parse_SkipsUnknownFields()
    {
        // 未知字段 99（varint）+ 一个合法文件，前者应被 SkipField 跳过。
        byte[] bytes = [.. VarintField(99, 7), .. BuildManifest()];
        var manifest = SophonChunkManifest.Parse(bytes);
        Assert.Single(manifest.Chunks);
    }
}
