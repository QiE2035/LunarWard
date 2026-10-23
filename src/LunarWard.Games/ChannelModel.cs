namespace LunarWard.Games;

/// <summary>
/// 渠道（区服）。本项目只暴露官方国服与 Bilibili；
/// Epic / Google Play 是历史遗留，<b>保留映射能力</b>以免抹掉别人写过的值，
/// 但不出现在 UI 的可选项里。见 <c>docs/04-FEATURES.md</c> §2.1。
/// </summary>
public enum GameChannel
{
    /// <summary>官方国服：<c>channel=1 cps=mihoyo sub_channel=0</c>。</summary>
    Official,

    /// <summary>B 站渠道：<c>channel=14 cps=bilibili sub_channel=0</c>。需要 <c>PCGameSDK.dll</c>。</summary>
    Bilibili,
}


/// <summary>
/// <c>config.ini</c> 里决定渠道的三个键。
/// </summary>
/// <remarks>
/// 官方国服 <c>1 / mihoyo / 0</c>、Bilibili <c>14 / bilibili / 0</c>、
/// Epic <c>1 / pcseaepic / 3</c>、Google Play <c>1 / pcgoogle / 6</c>。
/// 见 <c>docs/01-RESEARCH.md</c> §3.1。
/// </remarks>
public sealed record ChannelProfile(GameChannel Kind, int ChannelId, string Cps, int SubChannel)
{
    /// <summary>官方国服。</summary>
    public static readonly ChannelProfile Official = new(GameChannel.Official, 1, "mihoyo", 0);

    /// <summary>Bilibili 渠道。</summary>
    public static readonly ChannelProfile Bilibili = new(GameChannel.Bilibili, 14, "bilibili", 0);

    /// <summary>Epic（历史遗留，UI 不显示，仅用于保证读 → 写不抹掉）。</summary>
    public static readonly ChannelProfile Epic = new(GameChannel.Official, 1, "pcseaepic", 3);

    /// <summary>Google Play（历史遗留，同上）。</summary>
    public static readonly ChannelProfile GooglePlay = new(GameChannel.Official, 1, "pcgoogle", 6);


    /// <summary>本项目暴露的全部渠道（顺序即 UI 顺序）。</summary>
    public static IReadOnlyList<GameChannel> SupportedChannels { get; } = [GameChannel.Official, GameChannel.Bilibili];

    /// <summary>全部渠道档案（含历史遗留的两个）。</summary>
    public static IReadOnlyList<ChannelProfile> All { get; } = [Official, Bilibili, Epic, GooglePlay];


    /// <summary>UI 名称。</summary>
    public string DisplayName => Kind switch
    {
        GameChannel.Bilibili => "Bilibili",
        _ => Cps switch
        {
            "pcseaepic" => "Epic",
            "pcgoogle" => "Google Play",
            _ => "官方国服",
        },
    };


    /// <summary>渠道枚举（<see cref="Kind"/> 的别名，便于按语义读取）。</summary>
    public GameChannel Channel => Kind;


    /// <summary>该档案是否是其枚举值对应的"标准"档案（即 UI 选项）。</summary>
    public bool IsSelectable => this == Official || this == Bilibili;


    /// <summary>按枚举取档案（<see cref="GameChannel.Official"/> → <see cref="Official"/>）。</summary>
    public static ChannelProfile FromChannel(GameChannel channel) => channel switch
    {
        GameChannel.Bilibili => Bilibili,
        _ => Official,
    };


    /// <summary>
    /// 按三个键反查档案。匹配顺序：Bilibili → Epic → Google Play → 官方国服；
    /// <c>channel</c> 为 <c>0</c>（键不存在）时返回 <c>null</c>——
    /// 此时应视为"没有 config.ini"，而不是"已经切到官方"。
    /// </summary>
    public static ChannelProfile? FromConfig(int channelId, string? cps, int subChannel)
    {
        if (channelId == 0 && string.IsNullOrEmpty(cps))
        {
            return null;
        }

        foreach (var profile in All)
        {
            if (profile.ChannelId == channelId
                && profile.SubChannel == subChannel
                && string.Equals(profile.Cps, cps ?? "", StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }
        }

        // 键对得上但 cps 不认识：仍然按 channel 判定主导渠道，避免误报
        return channelId switch
        {
            14 => Bilibili,
            1 => Official,
            _ => null,
        };
    }


    /// <summary>决定渠道的三个 <c>config.ini</c> 键名。</summary>
    public static IReadOnlyList<string> ChannelKeyNames { get; } = ["channel", "sub_channel", "cps"];


    /// <summary>该渠道是否需要额外的前置条件（B 站插件）。</summary>
    public bool RequiresBilibiliPlugin => Kind == GameChannel.Bilibili;


    public override string ToString() => $"{DisplayName}(channel={ChannelId}, cps={Cps}, sub_channel={SubChannel})";
}


/// <summary>渠道相关的常量与查询。</summary>
public static class ChannelCatalog
{
    /// <summary>四个渠道在 <c>config.ini</c> 里用到的键名。</summary>
    public static IReadOnlyList<string> ChannelKeys => ChannelProfile.ChannelKeyNames;


    /// <summary>取档案（同 <see cref="ChannelProfile.FromChannel"/>）。</summary>
    public static ChannelProfile Get(GameChannel channel) => ChannelProfile.FromChannel(channel);


    /// <summary>
    /// 某个游戏定义支持哪些渠道。ZZZ 没有 B 站渠道
    /// （<c>env_emulation</c> 只有 PC / Epic / Android），见 <c>docs/04-FEATURES.md</c> §2.7。
    /// </summary>
    public static IReadOnlyList<GameChannel> SupportedFor(GameDefinition definition)
        => definition.SupportedChannels;
}
