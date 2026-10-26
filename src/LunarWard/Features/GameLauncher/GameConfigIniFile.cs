using System.Globalization;
using System.Text;
using LunarWard.Core;
using LunarWard.Platform;
using Microsoft.Extensions.Configuration;

namespace LunarWard.Features.GameLauncher;


/// <summary>
/// <c>config.ini</c> 的读写器。<b>保留未知键</b>：游戏版本更新可能新增字段，
/// 读 → 改 → 写不能把它们丢掉。
/// </summary>
/// <remarks>
/// <para>
/// 写入格式与游戏原生格式一致：<c>\n</c> 分隔、<c>key=value</c> 无空格、
/// 末行不带换行符。已知键的写出顺序见 <see cref="CanonicalOrder"/>。
/// </para>
/// <para>
/// <see cref="Parse(string?)"/> 与 <see cref="Generate"/> 都是<b>纯函数</b>（无文件 IO、
/// 无静态状态），可直接在单元测试里调用（U1/U2）。
/// </para>
/// </remarks>
public sealed class GameConfigIniFile
{
    /// <summary>
    /// 已知键的规范写出顺序。
    /// </summary>
    /// <remarks>
    /// 前四个键与 <c>docs/01-RESEARCH.md</c> §3.1 逐字节样例一致
    /// （<c>channel, cps, game_version, sub_channel</c>，即游戏原生格式）；
    /// 其余四个键是官方客户端/Starward 会写入的扩展键，排在后面。
    /// 需要别的顺序时给 <see cref="Generate"/> 传 <c>knownKeyOrder</c> 即可。
    /// </remarks>
    public static readonly IReadOnlyList<string> CanonicalOrder =
    [
        "channel",
        "cps",
        "game_version",
        "sub_channel",
        "sdk_version",
        "game_biz",
        "uapc",
        "downloading_mode",
    ];


    /// <summary>
    /// <c>docs/00-DECISIONS.md</c> D8 里列出的字段顺序
    /// （<c>channel, sub_channel, cps, game_version, sdk_version, game_biz, uapc, downloading_mode</c>）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="CanonicalOrder"/> 的差别只在 <c>cps</c>/<c>game_version</c>/<c>sub_channel</c>
    /// 三者的位置：D8 顺序是文档里枚举字段时的顺序，而游戏原生文本是
    /// <c>channel, cps, game_version, sub_channel</c>。两者都可用：
    /// <c>Generate(GameConfigIniFile.DocumentedOrder)</c>。
    /// </remarks>
    public static readonly IReadOnlyList<string> DocumentedOrder =
    [
        "channel",
        "sub_channel",
        "cps",
        "game_version",
        "sdk_version",
        "game_biz",
        "uapc",
        "downloading_mode",
    ];


    /// <summary>INI 的段落名。</summary>
    public const string SectionName = "General";

    /// <summary>行分隔符（与游戏原生格式一致，<b>不</b>用 <see cref="Environment.NewLine"/>）。</summary>
    public const string LineBreak = "\n";


    private const string SectionPrefix = SectionName + ":";


    private readonly List<string> _present = [];

    private readonly List<KeyValuePair<string, string>> _extraKeys = [];

    private readonly List<KeyValuePair<string, List<KeyValuePair<string, string>>>> _otherSections = [];


    public GameConfigIniFile()
    {
        General = new GameConfigIni();
    }


    private GameConfigIniFile(GameConfigIni general)
    {
        General = general;
    }


    /// <summary><c>[General]</c> 段的已知字段。</summary>
    public GameConfigIni General { get; set; }


    /// <summary>未知键（按文件里出现的顺序），如 <c>wpf_version</c> / <c>predownload</c>。</summary>
    public IReadOnlyList<KeyValuePair<string, string>> ExtraKeys => _extraKeys;


    /// <summary>读到的已知键名（即使值为空也保留，写回时不丢键）。</summary>
    public IReadOnlyList<string> PresentKeys => _present;


    /// <summary>非 <c>[General]</c> 的段落，原样保留。</summary>
    public IReadOnlyList<KeyValuePair<string, List<KeyValuePair<string, string>>>> OtherSections => _otherSections;


    // ─────────────────────────── 读 ───────────────────────────

    /// <summary>
    /// 从文本解析。<b>无副作用、不抛异常</b>：解析不了的内容一律当未知键原样保留。
    /// </summary>
    public static GameConfigIniFile Parse(string? text)
    {
        var document = new GameConfigIniFile();
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text ?? ""));
            var root = new ConfigurationBuilder().AddIniStream(stream).Build();
            document.Load(root);
        }
        catch (Exception)
        {
            // ini 解析器对残缺文本很宽容，走到这里说明文本彻底不可用——
            // 返回空文档，让调用方用正确值重写（config.ini 每个启动都会被重写）。
        }

        return document;
    }


    /// <summary>
    /// 读文件。文件不存在时返回空文档（<see cref="Read"/> 不抛异常）。
    /// </summary>
    /// <remarks>用官方的 <c>AddIniFile</c>，见 <c>docs/00-DECISIONS.md</c> D8。</remarks>
    public static GameConfigIniFile Read(string path)
    {
        var document = new GameConfigIniFile();
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return document;
            }

            var root = new ConfigurationBuilder().AddIniFile(path, optional: true).Build();
            document.Load(root);
        }
        catch (Exception)
        {
            // 读失败按"没有 config.ini"处理
        }

        return document;
    }


    /// <summary>读文件，失败返回 <c>false</c>（与 <see cref="Read"/> 的区别：能区分"文件不存在"）。</summary>
    public static bool TryRead(string path, out GameConfigIniFile document)
    {
        document = Read(path);
        return File.Exists(path);
    }


    /// <summary>从 <paramref name="path"/> 的所在目录读 <c>config.ini</c>。</summary>
    public static GameConfigIniFile ReadFromGameDirectory(string gameDirectory)
        => Read(GetConfigPath(gameDirectory));


    /// <summary>游戏目录下 <c>config.ini</c> 的路径。</summary>
    public static string GetConfigPath(string gameDirectory)
        => Path.Join(gameDirectory, "config.ini");


    private void Load(IConfigurationRoot root)
    {
        var sections = new List<KeyValuePair<string, List<KeyValuePair<string, string>>>>();

        foreach (var pair in root.AsEnumerable())
        {
            if (pair.Value is null)
            {
                // 段落节点本身（如 "General"）没有值
                continue;
            }

            var key = pair.Key;
            if (key.Equals(SectionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (key.StartsWith(SectionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                ApplyGeneralEntry(key[SectionPrefix.Length..], pair.Value);
                continue;
            }

            var delimiter = key.IndexOf(ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
            if (delimiter < 0)
            {
                // 出现在任何段落之前的键：当作 [General] 的未知键收下，不丢数据
                ApplyGeneralEntry(key, pair.Value);
                continue;
            }

            var sectionName = key[..delimiter];
            var sectionKey = key[(delimiter + ConfigurationPath.KeyDelimiter.Length)..];
            var section = sections.FirstOrDefault(x => x.Key.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
            if (section.Key is null)
            {
                section = new KeyValuePair<string, List<KeyValuePair<string, string>>>(
                    sectionName, []);
                sections.Add(section);
            }

            section.Value.Add(new KeyValuePair<string, string>(sectionKey, pair.Value));
        }

        _otherSections.Clear();
        _otherSections.AddRange(sections);
    }


    private void ApplyGeneralEntry(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "channel":
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var channel))
                {
                    General.Channel = channel;
                    MarkPresent(key);
                    return;
                }

                break;

            case "sub_channel":
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var subChannel))
                {
                    General.SubChannel = subChannel;
                    MarkPresent(key);
                    return;
                }

                break;

            case "cps":
                General.Cps = value;
                MarkPresent(key);
                return;

            case "game_version":
                if (string.IsNullOrWhiteSpace(value))
                {
                    MarkPresent(key);
                    return;
                }

                if (Version.TryParse(value.Trim(), out var gameVersion))
                {
                    General.GameVersion = gameVersion;
                    MarkPresent(key);
                    return;
                }

                break;

            case "sdk_version":
                if (string.IsNullOrWhiteSpace(value))
                {
                    MarkPresent(key);
                    return;
                }

                if (Version.TryParse(value.Trim(), out var sdkVersion))
                {
                    General.SdkVersion = sdkVersion;
                    MarkPresent(key);
                    return;
                }

                break;

            case "game_biz":
                General.GameBiz = new GameBiz(value);
                MarkPresent(key);
                return;

            case "uapc":
                General.Uapc = value;
                MarkPresent(key);
                return;

            case "downloading_mode":
                General.DownloadingMode = value;
                MarkPresent(key);
                return;
        }

        // 未知键（或已知键但值无法解析 → 原样保留文本，避免抹掉）
        SetExtra(key, value);
    }


    private void MarkPresent(string key)
    {
        if (!_present.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            _present.Add(key.ToLowerInvariant());
        }
    }


    // ─────────────────────────── 写 ───────────────────────────

    /// <summary>
    /// 强制把一个已知键标记为"存在"，即使它的值在当前文档里是空/零。
    /// </summary>
    /// <remarks>
    /// 用于"首次为某个渠道创建 config.ini"这类场景：需要写出 <c>game_biz=</c>
    /// 之外的显式键时用它。键名必须是 <see cref="CanonicalOrder"/> 之一。
    /// </remarks>
    public void AddKnownKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        MarkPresent(key);
    }


    /// <summary>
    /// 设置一个未知键（已存在的按忽略大小写替换并保留原位置）。
    /// </summary>
    public void SetExtra(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalized = value ?? "";
        for (var i = 0; i < _extraKeys.Count; i++)
        {
            if (_extraKeys[i].Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                _extraKeys[i] = new KeyValuePair<string, string>(_extraKeys[i].Key, normalized);
                return;
            }
        }

        _extraKeys.Add(new KeyValuePair<string, string>(key, normalized));
    }


    /// <summary>读取一个未知键。</summary>
    public bool TryGetExtra(string key, out string value)
    {
        foreach (var pair in _extraKeys)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = "";
        return false;
    }


    /// <summary>
    /// 生成 <c>config.ini</c> 文本：与游戏原生格式逐字节一致
    /// （<c>\n</c> 分隔、无空格、末行无换行）。
    /// </summary>
    /// <param name="knownKeyOrder">
    /// 已知键的写出顺序；<c>null</c> 用 <see cref="CanonicalOrder"/>。
    /// </param>
    public string Generate(IEnumerable<string>? knownKeyOrder = null)
    {
        var order = knownKeyOrder?.ToList() ?? CanonicalOrder.ToList();
        var builder = new StringBuilder();
        builder.Append('[').Append(SectionName).Append(']');
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in order)
        {
            if (!TryFormatKnown(key, out var value))
            {
                continue;
            }

            if (!written.Add(key))
            {
                continue;
            }

            builder.Append(LineBreak).Append(key).Append('=').Append(value);
        }

        // 未知键补在后面：不丢字段
        foreach (var pair in _extraKeys)
        {
            if (!written.Add(pair.Key))
            {
                continue;
            }

            builder.Append(LineBreak).Append(pair.Key).Append('=').Append(pair.Value);
        }

        // 其他段落原样保留（游戏的 config.ini 只有 [General]，这里只做兜底）
        foreach (var (sectionName, entries) in _otherSections)
        {
            builder.Append(LineBreak).Append('[').Append(sectionName).Append(']');
            foreach (var pair in entries)
            {
                builder.Append(LineBreak).Append(pair.Key).Append('=').Append(pair.Value);
            }
        }

        return builder.ToString();
    }


    private bool TryFormatKnown(string key, out string value)
    {
        var present = _present.Contains(key, StringComparer.OrdinalIgnoreCase);
        switch (key.ToLowerInvariant())
        {
            case "channel":
                value = General.Channel.ToString(CultureInfo.InvariantCulture);
                return present || General.Channel != 0;

            case "sub_channel":
                value = General.SubChannel.ToString(CultureInfo.InvariantCulture);
                return present || General.SubChannel != 0;

            case "cps":
                value = General.Cps ?? "";
                return present || value.Length > 0;

            case "game_version":
                value = General.GameVersion?.ToString() ?? "";
                return present || value.Length > 0;

            case "sdk_version":
                value = General.SdkVersion?.ToString() ?? "";
                return present || value.Length > 0;

            case "game_biz":
                value = General.GameBiz.Value;
                return present || value.Length > 0;

            case "uapc":
                value = General.Uapc ?? "";
                return present || value.Length > 0;

            case "downloading_mode":
                value = General.DownloadingMode ?? "";
                return present || value.Length > 0;

            default:
                value = "";
                return false;
        }
    }


    /// <summary>原子写入（<c>.tmp</c> → <c>fsync</c> → <c>rename</c>）。</summary>
    public void Write(string path) => AtomicFile.WriteAllText(path, Generate());


    /// <summary>写到游戏目录下的 <c>config.ini</c>。</summary>
    public void WriteToGameDirectory(string gameDirectory) => Write(GetConfigPath(gameDirectory));


    // ─────────────────────────── 渠道 ───────────────────────────

    /// <summary>空文档（只有 <c>[General]</c>）。</summary>
    public static GameConfigIniFile CreateEmpty() => new();


    /// <summary>
    /// 为某个渠道新建文档。已知键只包含原生格式的四个
    /// （<c>channel</c> / <c>cps</c> / <c>game_version</c> / <c>sub_channel</c>），
    /// 因此 <see cref="Generate"/> 的输出与游戏原生文本逐字节一致。
    /// </summary>
    public static GameConfigIniFile Create(ChannelProfile profile, string? gameVersion = null)
        => CreateEmpty().WithChannel(profile, gameVersion);


    /// <summary>
    /// 返回一份应用了渠道设置的副本：<b>保留</b>原有未知键与其他段落，
    /// 只改 <c>channel</c> / <c>cps</c> / <c>sub_channel</c>（以及传入时的 <c>game_version</c>）。
    /// </summary>
    public GameConfigIniFile WithChannel(ChannelProfile profile, string? gameVersion = null)
    {
        // 上游 GameConfigIni 没有 Clone()（它只被 Ini 配置绑定使用）；
        // 这里为「读→改→写」显式做一次浅拷贝，字段集合与上游一致。
        var copy = new GameConfigIniFile(new GameConfigIni
        {
            Channel = General.Channel,
            SubChannel = General.SubChannel,
            Cps = General.Cps,
            GameVersion = General.GameVersion,
            SdkVersion = General.SdkVersion,
            GameBiz = General.GameBiz,
            Uapc = General.Uapc,
            DownloadingMode = General.DownloadingMode,
        });
        copy._present.AddRange(_present);
        copy._extraKeys.AddRange(_extraKeys);
        copy._otherSections.AddRange(_otherSections);

        copy.General.Channel = profile.ChannelId;
        copy.General.Cps = profile.Cps;
        copy.General.SubChannel = profile.SubChannel;
        copy.MarkPresent("channel");
        copy.MarkPresent("cps");
        copy.MarkPresent("sub_channel");

        // 若原文件里这些键的值无法解析（被收成未知键），现在已被显式值取代，丢掉旧文本
        copy._extraKeys.RemoveAll(x => ChannelProfile.ChannelKeyNames.Contains(x.Key));

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            if (Version.TryParse(gameVersion.Trim(), out var version))
            {
                copy.General.GameVersion = version;
            }

            copy.MarkPresent("game_version");
        }

        return copy;
    }


    /// <summary>当前文档表示的渠道（无法匹配四个已知渠道时返回 <c>null</c>）。</summary>
    public ChannelProfile? GetChannelProfile()
        => ChannelProfile.FromConfig(General.Channel, General.Cps, General.SubChannel);


    public override string ToString() => Generate();
}
