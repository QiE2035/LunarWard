using System.Text;
using LunarWard.Installer;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>手写 protobuf 编码，喂给 <c>SophonPatchManifest.Parse</c>（对应上游 Sophon.proto）。</summary>
public sealed class SophonPatchManifestTests
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

    [Fact]
    public void Parse_ReadsPatchesDeleteTagsAndCompressMode()
    {
        // SophonPatch：id / tag / patch_file_size=128 / original_file_size=4096 / original_file_md5
        byte[] patch = [.. UTF8(1, "p0"), .. UTF8(2, "1.0.0"), .. VarintField(4, 128), .. VarintField(9, 4096), .. UTF8(10, "orig-md5")];
        // SophonPatchInfo：tag + patch
        byte[] info = [.. UTF8(1, "1.0.0"), .. BytesField(2, patch)];
        // SophonPatchFile：file / size=4096 / md5 / 一个 info
        byte[] file = [.. UTF8(1, "Game/Data/a.pak"), .. VarintField(2, 4096), .. UTF8(3, "new-md5"), .. BytesField(4, info)];
        // SophonPatchDeleteTag：只有 tag（delete_collection 缺省）
        byte[] deleteTag = UTF8(1, "0.9.0");

        byte[] manifestBytes = [.. BytesField(1, file), .. BytesField(2, deleteTag), .. VarintField(3, 3)];

        var manifest = SophonPatchManifest.Parse(manifestBytes);

        Assert.Equal(3, manifest.CompressMode);
        var patchFile = Assert.Single(manifest.Patches);
        Assert.Equal("Game/Data/a.pak", patchFile.File);
        Assert.Equal(4096, patchFile.Size);
        Assert.Equal("new-md5", patchFile.Md5);
        var patchInfo = Assert.Single(patchFile.Patches);
        Assert.Equal("1.0.0", patchInfo.Tag);
        Assert.NotNull(patchInfo.Patch);
        Assert.Equal("p0", patchInfo.Patch!.Id);
        Assert.Equal(128, patchInfo.Patch.PatchFileSize);
        Assert.Equal(4096, patchInfo.Patch.OriginalFileSize);
        Assert.Equal("orig-md5", patchInfo.Patch.OriginalFileMd5);

        var deleted = Assert.Single(manifest.DeleteTags);
        Assert.Equal("0.9.0", deleted.Tag);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyManifest()
    {
        var manifest = SophonPatchManifest.Parse([]);
        Assert.Empty(manifest.Patches);
        Assert.Empty(manifest.DeleteTags);
        Assert.Equal(0, manifest.CompressMode);
    }
}
