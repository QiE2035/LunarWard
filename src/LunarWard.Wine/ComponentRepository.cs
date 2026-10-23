using System.Text.Json;
using System.Text.Json.Serialization;

namespace LunarWard.Wine;

/// <summary>
/// an-anime-team 组件索引模型（<c>components.json</c> + <c>wine/&lt;group&gt;.json</c>）。
/// </summary>
/// <remarks>见 <c>docs/01-RESEARCH.md</c> §6.3。</remarks>
public sealed class ComponentIndex
{
    [JsonPropertyName("wine")]
    public List<ComponentGroup> Wine { get; set; } = [];

    [JsonPropertyName("dxvk")]
    public List<ComponentGroup> Dxvk { get; set; } = [];
}


/// <summary>一个组件分组（如 <c>spritz-wine-cachyos</c>）。</summary>
public sealed class ComponentGroup
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}


/// <summary>单个组件版本条目。</summary>
public sealed class ComponentEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    /// <summary>下载后的可执行文件相对路径映射，例如 <c>bin/wine</c>。</summary>
    [JsonPropertyName("files")]
    public ComponentFiles? Files { get; set; }

    [JsonPropertyName("features")]
    public ComponentFeatures? Features { get; set; }
}


public sealed class ComponentFiles
{
    [JsonPropertyName("wine")]
    public string? Wine { get; set; }

    [JsonPropertyName("wine64")]
    public string? Wine64 { get; set; }

    [JsonPropertyName("wineserver")]
    public string? Wineserver { get; set; }

    [JsonPropertyName("wineboot")]
    public string? Wineboot { get; set; }
}


public sealed class ComponentFeatures
{
    [JsonPropertyName("arch")]
    public string? Arch { get; set; }

    /// <summary>命令模板，支持 <c>%build%</c> <c>%prefix%</c> <c>%temp%</c> <c>%launcher%</c> <c>%game%</c>。</summary>
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; set; }

    [JsonPropertyName("recommended")]
    public bool Recommended { get; set; }
}


/// <summary>
/// 组件索引读取。目录结构：
/// <code>
/// &lt;data&gt;/components/
/// ├── components.json
/// ├── wine/&lt;group&gt;.json
/// └── dxvk/&lt;group&gt;.json
/// </code>
/// </summary>
public sealed class ComponentRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };


    private readonly string _root;


    public ComponentRepository(string root)
    {
        _root = root;
    }


    public string Root => _root;


    /// <summary>读取 <c>components.json</c>；不存在时返回空索引。</summary>
    public ComponentIndex ReadIndex()
    {
        var path = Path.Join(_root, "components.json");
        if (!File.Exists(path))
        {
            return new ComponentIndex();
        }

        try
        {
            return JsonSerializer.Deserialize<ComponentIndex>(File.ReadAllText(path), Options) ?? new ComponentIndex();
        }
        catch (JsonException)
        {
            return new ComponentIndex();
        }
    }


    /// <summary>读取某分组下的全部版本条目。</summary>
    public IReadOnlyList<ComponentEntry> ReadGroup(string kind, string group)
    {
        // kind: "wine" | "dxvk"
        var path = Path.Join(_root, kind, group + ".json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ComponentEntry>>(File.ReadAllText(path), Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }


    /// <summary>
    /// 按组件名查找条目。组件名是全局唯一的（例如
    /// <c>spritz-wine-cachyos-wow64-10.0-11</c>），所以要先枚举所有分组。
    /// </summary>
    public (ComponentEntry Entry, string Group)? Find(string kind, string componentName)
    {
        var index = ReadIndex();
        var groups = kind == "wine" ? index.Wine : index.Dxvk;
        foreach (var group in groups)
        {
            foreach (var entry in ReadGroup(kind, group.Name))
            {
                if (string.Equals(entry.Name, componentName, StringComparison.Ordinal))
                {
                    return (entry, group.Name);
                }
            }
        }

        // 索引缺失时直接扫目录（用户手动放文件的情况）
        var dir = Path.Join(_root, kind);
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                var groupName = Path.GetFileNameWithoutExtension(file);
                foreach (var entry in ReadGroup(kind, groupName))
                {
                    if (string.Equals(entry.Name, componentName, StringComparison.Ordinal))
                    {
                        return (entry, groupName);
                    }
                }
            }
        }

        return null;
    }
}
