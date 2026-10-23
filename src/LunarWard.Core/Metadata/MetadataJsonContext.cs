using LunarWard.Core.Metadata.Github;
using System.Text.Json.Serialization;

namespace LunarWard.Core.Metadata;

[JsonSerializable(typeof(ReleaseVersion))]
[JsonSerializable(typeof(GithubRelease))]
[JsonSerializable(typeof(List<GithubRelease>))]
[JsonSerializable(typeof(GithubMarkdownRequest))]
internal partial class MetadataJsonContext : JsonSerializerContext
{

}
