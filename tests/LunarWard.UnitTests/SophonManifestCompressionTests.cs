using System.Text;
using LunarWard.Installer;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>Sophon 清单的 zstd 解压辅助（<c>SophonManifestCompression</c>）。</summary>
public sealed class SophonManifestCompressionTests
{
    private static byte[] Zstd(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var compressor = new ZstdSharp.CompressionStream(output, level: 3, leaveOpen: true))
        {
            compressor.Write(payload);
        }

        return output.ToArray();
    }

    [Fact]
    public void DecompressZstd_Span_RoundTrips()
    {
        byte[] payload = Encoding.UTF8.GetBytes(new string('m', 4096));
        Assert.Equal(payload, SophonManifestCompression.DecompressZstd(Zstd(payload)));
    }

    [Fact]
    public void DecompressZstd_Stream_RoundTrips()
    {
        byte[] payload = Enumerable.Range(0, 2048).Select(i => (byte)(i % 251)).ToArray();
        using var input = new MemoryStream(Zstd(payload));
        Assert.Equal(payload, SophonManifestCompression.DecompressZstd(input));
    }

    [Fact]
    public void DecompressZstd_Stream_DoesNotCloseInput()
    {
        using var input = new MemoryStream(Zstd([1, 2, 3, 4]));
        _ = SophonManifestCompression.DecompressZstd(input);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void DecompressZstd_EmptyPayload_RoundTrips()
    {
        Assert.Empty(SophonManifestCompression.DecompressZstd(Zstd([])));
    }
}
