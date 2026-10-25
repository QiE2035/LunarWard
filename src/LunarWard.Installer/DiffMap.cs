using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LunarWard.Installer;

internal class DiffMap
{

    [JsonPropertyName("diff_map")]
    public List<DiffMapItem> DiffMapItems { get; set; } = default!;

}


internal class DiffMapItem
{

    [JsonPropertyName("source_file_name")]
    public string SourceFileName { get; set; } = default!;

    [JsonPropertyName("source_file_md5")]
    public string SourceFileMd5 { get; set; } = default!;

    [JsonPropertyName("source_file_size")]
    public long SourceFileSize { get; set; }

    [JsonPropertyName("target_file_name")]
    public string TargetFileName { get; set; } = default!;

    [JsonPropertyName("target_file_md5")]
    public string TargetFileMd5 { get; set; } = default!;

    [JsonPropertyName("target_file_size")]
    public long TargetFileSize { get; set; }

    [JsonPropertyName("patch_file_name")]
    public string PatchFileName { get; set; } = default!;

    [JsonPropertyName("patch_file_md5")]
    public string PatchFileMd5 { get; set; } = default!;

    [JsonPropertyName("patch_file_size")]
    public long PatchFileSize { get; set; }

}



internal class HDiffFile
{
    [JsonPropertyName("remoteName")]
    public string RemoteName { get; set; } = default!;
}