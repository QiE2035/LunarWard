using LunarWard.Core;
using LunarWard.Core.HoYoPlay;

namespace LunarWard.Installer;

/// <summary>
/// Chunk 下载模式的清单（对应上游 <c>Sophon.proto</c> 的 <c>SophonChunkManifest</c>）。
///
/// <para>上游由 <c>Google.Protobuf</c> 的 <c>protoc</c> 生成，本项目删除了
/// <c>*.proto</c> 与 protobuf 依赖，改用 <see cref="ProtoReader"/> 手写解析。
/// 字段号与 <c>Sophon.proto</c> 严格对应，见各属性注释。</para>
/// </summary>
public class SophonChunkManifest
{
    /// <summary><c>repeated SophonChunkFile chuncks = 1;</c>（上游拼写如此）。</summary>
    public List<SophonChunkFile> Chunks { get; } = new();


    public static SophonChunkManifest Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonChunkManifest();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.Chunks.Add(SophonChunkFile.Parse(reader.ReadBytes()));
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>Chunk 模式游戏文件（<c>SophonChunkFile</c>）。</summary>
public class SophonChunkFile
{
    /// <summary><c>string file = 1;</c> 相对安装目录的路径。</summary>
    public string File { get; set; } = "";

    /// <summary><c>repeated SophonChunk chunks = 2;</c></summary>
    public List<SophonChunk> Chunks { get; } = new();

    /// <summary><c>bool is_folder = 3;</c></summary>
    public bool IsFolder { get; set; }

    /// <summary><c>int64 size = 4;</c></summary>
    public long Size { get; set; }

    /// <summary><c>string md5 = 5;</c></summary>
    public string Md5 { get; set; } = "";


    internal static SophonChunkFile Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonChunkFile();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.File = reader.ReadString();
                    break;
                case 2 when wireType == 2:
                    result.Chunks.Add(SophonChunk.Parse(reader.ReadBytes()));
                    break;
                case 3 when wireType == 0:
                    result.IsFolder = reader.ReadBool();
                    break;
                case 4 when wireType == 0:
                    result.Size = reader.ReadInt64();
                    break;
                case 5 when wireType == 2:
                    result.Md5 = reader.ReadString();
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>Chunk 模式文件块（<c>SophonChunk</c>）。</summary>
public class SophonChunk
{
    /// <summary><c>string id = 1;</c></summary>
    public string Id { get; set; } = "";

    /// <summary><c>string uncompressed_md5 = 2;</c></summary>
    public string UncompressedMd5 { get; set; } = "";

    /// <summary><c>int64 offset = 3;</c> 在该文件内的偏移。</summary>
    public long Offset { get; set; }

    /// <summary><c>int64 compressed_size = 4;</c></summary>
    public long CompressedSize { get; set; }

    /// <summary><c>int64 uncompressed_size = 5;</c></summary>
    public long UncompressedSize { get; set; }

    /// <summary><c>string compressed_md5 = 7;</c></summary>
    public string CompressedMd5 { get; set; } = "";


    internal static SophonChunk Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonChunk();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.Id = reader.ReadString();
                    break;
                case 2 when wireType == 2:
                    result.UncompressedMd5 = reader.ReadString();
                    break;
                case 3 when wireType == 0:
                    result.Offset = reader.ReadInt64();
                    break;
                case 4 when wireType == 0:
                    result.CompressedSize = reader.ReadInt64();
                    break;
                case 5 when wireType == 0:
                    result.UncompressedSize = reader.ReadInt64();
                    break;
                case 7 when wireType == 2:
                    result.CompressedMd5 = reader.ReadString();
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>Patch 更新模式的清单（<c>SophonPatchManifest</c>）。</summary>
public class SophonPatchManifest
{
    /// <summary><c>repeated SophonPatchFile patches = 1;</c> 新版本所有文件。</summary>
    public List<SophonPatchFile> Patches { get; } = new();

    /// <summary><c>repeated SophonPatchDeleteTag delete_tags = 2;</c></summary>
    public List<SophonPatchDeleteTag> DeleteTags { get; } = new();

    /// <summary><c>int32 compress_mode = 3;</c></summary>
    public int CompressMode { get; set; }


    public static SophonPatchManifest Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonPatchManifest();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.Patches.Add(SophonPatchFile.Parse(reader.ReadBytes()));
                    break;
                case 2 when wireType == 2:
                    result.DeleteTags.Add(SophonPatchDeleteTag.Parse(reader.ReadBytes()));
                    break;
                case 3 when wireType == 0:
                    result.CompressMode = reader.ReadInt32();
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>Patch 模式下的一个目标文件（<c>SophonPatchFile</c>）。</summary>
public class SophonPatchFile
{
    /// <summary><c>string file = 1;</c></summary>
    public string File { get; set; } = "";

    /// <summary><c>int64 size = 2;</c></summary>
    public long Size { get; set; }

    /// <summary><c>string md5 = 3;</c></summary>
    public string Md5 { get; set; } = "";

    /// <summary><c>repeated SophonPatchInfo patches = 4;</c> 空表示不需要更新。</summary>
    public List<SophonPatchInfo> Patches { get; } = new();


    internal static SophonPatchFile Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonPatchFile();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.File = reader.ReadString();
                    break;
                case 2 when wireType == 0:
                    result.Size = reader.ReadInt64();
                    break;
                case 3 when wireType == 2:
                    result.Md5 = reader.ReadString();
                    break;
                case 4 when wireType == 2:
                    result.Patches.Add(SophonPatchInfo.Parse(reader.ReadBytes()));
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>按"更新前版本 tag"索引的补丁（<c>SophonPatchInfo</c>）。</summary>
public class SophonPatchInfo
{
    /// <summary><c>string tag = 1;</c> 更新前游戏版本。</summary>
    public string Tag { get; set; } = "";

    /// <summary><c>SophonPatch patch = 2;</c></summary>
    public SophonPatch? Patch { get; set; }


    internal static SophonPatchInfo Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonPatchInfo();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.Tag = reader.ReadString();
                    break;
                case 2 when wireType == 2:
                    result.Patch = SophonPatch.Parse(reader.ReadBytes());
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>一个文件的差量补丁（<c>SophonPatch</c>）。</summary>
public class SophonPatch
{
    /// <summary><c>string id = 1;</c></summary>
    public string Id { get; set; } = "";

    /// <summary><c>string tag = 2;</c></summary>
    public string Tag { get; set; } = "";

    /// <summary><c>string build_id = 3;</c></summary>
    public string BuildId { get; set; } = "";

    /// <summary><c>int64 patch_file_size = 4;</c></summary>
    public long PatchFileSize { get; set; }

    /// <summary><c>string patch_file_md5 = 5;</c></summary>
    public string PatchFileMd5 { get; set; } = "";

    /// <summary><c>int64 patch_offset = 6;</c> 当前补丁在 patch 文件里的偏移。</summary>
    public long PatchOffset { get; set; }

    /// <summary><c>int64 patch_length = 7;</c></summary>
    public long PatchLength { get; set; }

    /// <summary>
    /// <c>string original_file_name = 8;</c>
    /// 非空表示需要用 hdiffpatch 合并；为空表示这是新文件，补丁本身就是原文件内容，直接复制。
    /// </summary>
    public string OriginalFileName { get; set; } = "";

    /// <summary><c>int64 original_file_size = 9;</c></summary>
    public long OriginalFileSize { get; set; }

    /// <summary><c>string original_file_md5 = 10;</c></summary>
    public string OriginalFileMd5 { get; set; } = "";


    internal static SophonPatch Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonPatch();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.Id = reader.ReadString();
                    break;
                case 2 when wireType == 2:
                    result.Tag = reader.ReadString();
                    break;
                case 3 when wireType == 2:
                    result.BuildId = reader.ReadString();
                    break;
                case 4 when wireType == 0:
                    result.PatchFileSize = reader.ReadInt64();
                    break;
                case 5 when wireType == 2:
                    result.PatchFileMd5 = reader.ReadString();
                    break;
                case 6 when wireType == 0:
                    result.PatchOffset = reader.ReadInt64();
                    break;
                case 7 when wireType == 0:
                    result.PatchLength = reader.ReadInt64();
                    break;
                case 8 when wireType == 2:
                    result.OriginalFileName = reader.ReadString();
                    break;
                case 9 when wireType == 0:
                    result.OriginalFileSize = reader.ReadInt64();
                    break;
                case 10 when wireType == 2:
                    result.OriginalFileMd5 = reader.ReadString();
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>Patch 模式需要删除的文件（<c>SophonPatchDeleteTag</c>）。</summary>
public class SophonPatchDeleteTag
{
    /// <summary><c>string tag = 1;</c> 更新前游戏版本。</summary>
    public string Tag { get; set; } = "";

    /// <summary><c>SophonPatchDeleteFileCollection delete_collection = 2;</c></summary>
    public SophonPatchDeleteFileCollection? DeleteCollection { get; set; }


    internal static SophonPatchDeleteTag Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonPatchDeleteTag();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.Tag = reader.ReadString();
                    break;
                case 2 when wireType == 2:
                    result.DeleteCollection = SophonPatchDeleteFileCollection.Parse(reader.ReadBytes());
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>删除文件集合（<c>SophonPatchDeleteFileCollection</c>）。</summary>
public class SophonPatchDeleteFileCollection
{
    /// <summary><c>repeated SophonPatchDeleteFile delete_files = 1;</c></summary>
    public List<SophonPatchDeleteFile> DeleteFiles { get; } = new();


    internal static SophonPatchDeleteFileCollection Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonPatchDeleteFileCollection();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.DeleteFiles.Add(SophonPatchDeleteFile.Parse(reader.ReadBytes()));
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>Patch 模式需要删除的一个文件（<c>SophonPatchDeleteFile</c>）。</summary>
public class SophonPatchDeleteFile
{
    /// <summary><c>string file = 1;</c></summary>
    public string File { get; set; } = "";

    /// <summary><c>int64 size = 2;</c></summary>
    public long Size { get; set; }

    /// <summary><c>string md5 = 3;</c></summary>
    public string Md5 { get; set; } = "";


    internal static SophonPatchDeleteFile Parse(ReadOnlySpan<byte> bytes)
    {
        var result = new SophonPatchDeleteFile();
        var reader = new ProtoReader(bytes);
        while (!reader.End)
        {
            var field = reader.ReadTag(out var wireType);
            if (field == 0)
            {
                break;
            }

            switch (field)
            {
                case 1 when wireType == 2:
                    result.File = reader.ReadString();
                    break;
                case 2 when wireType == 0:
                    result.Size = reader.ReadInt64();
                    break;
                case 3 when wireType == 2:
                    result.Md5 = reader.ReadString();
                    break;
                default:
                    reader.SkipField(wireType);
                    break;
            }
        }

        return result;
    }
}


/// <summary>
/// 解析 <c>chunk</c> 清单时必须先解压（清单文件本身是 zstd 帧）。
/// </summary>
public static class SophonManifestCompression
{
    /// <summary>
    /// 用 <c>ZstdSharp.Port</c> 解压单帧 zstd 数据。
    /// </summary>
    public static byte[] DecompressZstd(Stream compressed)
    {
        using var decompression = new ZstdSharp.DecompressionStream(compressed, leaveOpen: true);
        using var output = new MemoryStream();
        decompression.CopyTo(output);
        return output.ToArray();
    }


    /// <summary>解压内存中的单帧 zstd 数据。</summary>
    public static byte[] DecompressZstd(ReadOnlySpan<byte> compressed)
    {
        var buffer = compressed.ToArray();
        using var input = new MemoryStream(buffer, writable: false);
        return DecompressZstd(input);
    }
}
