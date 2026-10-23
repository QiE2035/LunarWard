using LunarWard.App.Localization;
using LunarWard.Launch;
using LunarWard.Games;
using LunarWard.Wine;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 游戏设置页的下拉框选项。
/// </summary>
/// <remarks>
/// <para>
/// Avalonia 的 <c>ComboBox</c> 在没有 <c>ItemTemplate</c> 时用 <see cref="object.ToString"/> 显示，
/// 所以这些选项统一把本地化文本放进 <c>Display</c> 并重写 <c>ToString</c>——
/// 页面里不需要再为每个下拉框写模板（Starward 的 <c>ComboBoxItem</c> 等价物）。
/// </para>
/// <para>
/// 全部选项都在页面构造时创建；语言切换会重建页面（<c>ShellViewModel.RebuildPage</c>），
/// 因此这里不需要监听 <see cref="AppStrings.PropertyChanged"/>。
/// </para>
/// </remarks>
public sealed record GameSettingChannelOption(string Key, string Display)
{
    private static AppStrings Strings => AppStrings.Instance;


    /// <summary>官方国服的键（写回 <c>profile.channel</c>）。</summary>
    public const string Official = "Official";

    /// <summary>Bilibili 渠道的键。</summary>
    public const string Bilibili = "Bilibili";


    public static GameSettingChannelOption FromChannel(GameChannel channel) => channel switch
    {
        GameChannel.Bilibili => new(Bilibili, Strings["GameSetting.ChannelBilibili"]),
        _ => new(Official, Strings["GameSetting.ChannelOfficial"]),
    };


    public override string ToString() => Display;
}


/// <summary>同步原语选项（<c>WineSync</c> 的显示名）。</summary>
public sealed record GameSettingSyncOption(WineSync Value, string Display)
{
    private static AppStrings Strings => AppStrings.Instance;


    public static IReadOnlyList<GameSettingSyncOption> All { get; } =
    [
        new(WineSync.None, Strings["GameSetting.SyncNone"]),
        new(WineSync.ESync, "ESync"),
        new(WineSync.FSync, "FSync"),
        new(WineSync.NTSync, "NTSync"),
    ];


    public override string ToString() => Display;
}


/// <summary>HUD 选项（值写回 <c>enhancements.hud</c>）。</summary>
public sealed record GameSettingHudOption(string Value, string Display)
{
    private static AppStrings Strings => AppStrings.Instance;


    /// <summary>自定义 HUD 串也在这里（Starward 的 ComboBox 允许输入之外的既有值）。</summary>
    public static IReadOnlyList<GameSettingHudOption> CreateAll(string? current)
    {
        var list = new List<GameSettingHudOption>
        {
            new("None", Strings["GameSetting.HudNone"]),
            new("Fps", "FPS"),
            new("Full", Strings["GameSetting.HudFull"]),
        };

        if (!string.IsNullOrWhiteSpace(current)
            && !list.Any(x => string.Equals(x.Value, current, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(new(current, current));
        }

        return list;
    }


    public override string ToString() => Display;
}


/// <summary>触发方式（<c>Auto</c> = 伴随启动 / <c>Manual</c> = 手动）。</summary>
public sealed record GameSettingTriggerOption(Trigger Value, string Display)
{
    private static AppStrings Strings => AppStrings.Instance;


    public static IReadOnlyList<GameSettingTriggerOption> All { get; } =
    [
        new(Trigger.Auto, Strings["GameSetting.TriggerAuto"]),
        new(Trigger.Manual, Strings["GameSetting.TriggerManual"]),
    ];


    public override string ToString() => Display;
}


/// <summary>游戏退出后的处理策略。</summary>
public sealed record GameSettingExitOption(ExitPolicy Value, string Display)
{
    private static AppStrings Strings => AppStrings.Instance;


    public static IReadOnlyList<GameSettingExitOption> All { get; } =
    [
        new(ExitPolicy.Kill, Strings["GameSetting.ExitKill"]),
        new(ExitPolicy.Keep, Strings["GameSetting.ExitKeep"]),
    ];


    public override string ToString() => Display;
}


/// <summary>三态环境变量（继承 / 覆盖 / 删除）。</summary>
public sealed record GameSettingEnvKindOption(EnvPatchKind Value, string Display)
{
    private static AppStrings Strings => AppStrings.Instance;


    public static IReadOnlyList<GameSettingEnvKindOption> All { get; } =
    [
        new(EnvPatchKind.Inherit, Strings["GameSetting.EnvKindInherit"]),
        new(EnvPatchKind.Override, Strings["GameSetting.EnvKindOverride"]),
        new(EnvPatchKind.Remove, Strings["GameSetting.EnvKindRemove"]),
    ];


    public override string ToString() => Display;
}


/// <summary>
/// 十层合并结果的一行（键 / 值 / 来源层）。
/// </summary>
/// <remarks>
/// 空字符串必须是<b>可见</b>的：<c>DISPLAY=""</c>（winewayland）与"没有这个键"是两种行为，
/// 见 <c>docs/04-FEATURES.md</c> §1.4。
/// </remarks>
public sealed record GameSettingEnvRow(string Key, string Value, string Source)
{
    public bool IsEmpty => Value.Length == 0;


    public string DisplayValue => IsEmpty
        ? AppStrings.Instance["GameSetting.EnvEmptyValue"]
        : Value;
}
