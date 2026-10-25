using LunarWard.Installer;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// Sophon chunk manifest 的 protobuf 解码（M-08 的纯函数部分）。
/// 手工编码字节，不依赖上游抓包样本。
/// </summary>
public class SophonManifestTests
{
    // ── 极简 protobuf 编码器 ───────────────────────────────────────────────

    private static List<byte> Varint(long value)
    {
        var bytes = new List<byte>();
        var v = (ulong)value;
        while (v >= 0x80)
        {
            bytes.Add((byte)(v | 0x80));
            v >>= 7;
        }

        bytes.Add((byte)v);
        return bytes;
    }

    private static List<byte> Tag(int field, int wireType) => Varint((field << 3) | wireType);

    private static List<byte> Str(int field, string value)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(value);
        var bytes = Tag(field, 2);
        bytes.AddRange(Varint(utf8.Length));
        bytes.AddRange(utf8);
        return bytes;
    }

    private static List<byte> Int(int field, long value)
    {
        var bytes = Tag(field, 0);
        bytes.AddRange(Varint(value));
        return bytes;
    }

    private static List<byte> Msg(int field, ReadOnlySpan<byte> body)
    {
        var bytes = Tag(field, 2);
        bytes.AddRange(Varint(body.Length));
        bytes.AddRange(body.ToArray());
        return bytes;
    }

    private static List<byte> Msg(int field, List<byte> body) => Msg(field, body.ToArray());

    private static byte[] Concat(params ReadOnlySpan<List<byte>> parts)
    {
        var result = new List<byte>();
        foreach (var part in parts)
        {
            result.AddRange(part);
        }

        return result.ToArray();
    }

    // ── 用例 ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SingleFileWithChunk_ReadsAllFields()
    {
        var chunk = Concat(
            Str(1, "chunk-id-1"),
            Str(2, "uncompressed-md5"),
            Int(3, 4096),
            Int(4, 2048),
            Int(5, 8192),
            Str(7, "compressed-md5"));

        var file = Concat(
            Str(1, "GenshinImpact.exe"),
            Msg(2, chunk),
            Int(3, 1),
            Int(4, 123456),
            Str(5, "file-md5"));

        var manifest = SophonChunkManifest.Parse(Concat(Msg(1, file)));

        var entry = Assert.Single(manifest.Chunks);
        Assert.Equal("GenshinImpact.exe", entry.File);
        Assert.True(entry.IsFolder);
        Assert.Equal(123456, entry.Size);
        Assert.Equal("file-md5", entry.Md5);

        var piece = Assert.Single(entry.Chunks);
        Assert.Equal("chunk-id-1", piece.Id);
        Assert.Equal("uncompressed-md5", piece.UncompressedMd5);
        Assert.Equal(4096, piece.Offset);
        Assert.Equal(2048, piece.CompressedSize);
        Assert.Equal(8192, piece.UncompressedSize);
        Assert.Equal("compressed-md5", piece.CompressedMd5);
    }

    [Fact]
    public void Parse_MultipleFiles_KeepsOrder()
    {
        var manifest = SophonChunkManifest.Parse(Concat(
            Msg(1, Str(1, "a.bin")),
            Msg(1, Str(1, "b.bin")),
            Msg(1, Str(1, "c.bin"))));

        Assert.Equal(["a.bin", "b.bin", "c.bin"], manifest.Chunks.Select(c => c.File));
    }

    [Fact]
    public void Parse_UnknownFieldAndMismatchedWireType_AreSkipped()
    {
        // field 99 不在 schema 里；field 1 用 varint（应为 length-delimited）→ 都应被跳过
        var manifest = SophonChunkManifest.Parse(Concat(
            Int(99, 7),
            Int(1, 3),
            Msg(1, Str(1, "only-valid.bin"))));

        var entry = Assert.Single(manifest.Chunks);
        Assert.Equal("only-valid.bin", entry.File);
    }

    [Fact]
    public void Parse_Empty_ReturnsEmptyManifest()
    {
        Assert.Empty(SophonChunkManifest.Parse([]).Chunks);
    }
}
