namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 渠道前置条件校验结果。
/// </summary>
/// <param name="Ok">
/// 是否可以按该渠道启动。<b>只有</b>不可自动修复的条件（非国服、插件缺失、游戏不支持该渠道）
/// 会让它为 <c>false</c>。
/// </param>
/// <param name="Error">失败原因（<see cref="Ok"/> 为 <c>true</c> 时通常为 <c>null</c>）。</param>
/// <param name="NeedsPluginDownload">失败原因是"缺少 B 站插件"，UI 应显示"下载插件"按钮。</param>
public sealed record ChannelCheckResult(bool Ok, string? Error, bool NeedsPluginDownload)
{
    /// <summary>
    /// <c>config.ini</c> 还没写成该渠道（缺失或值不对）。<b>不是失败</b>——
    /// 启动引擎会在 <c>Prepare</c> 相位重写它。见 <c>docs/04-FEATURES.md</c> §2.5 条件 3。
    /// </summary>
    public bool NeedsConfigRewrite { get; init; }


    /// <summary>给 UI 的提示（可空）。</summary>
    public string? Warning { get; init; }


    /// <summary>成功。</summary>
    public static ChannelCheckResult Success(bool needsConfigRewrite = false, string? warning = null)
        => new(true, null, false)
        {
            NeedsConfigRewrite = needsConfigRewrite,
            Warning = warning,
        };


    /// <summary>硬失败。</summary>
    public static ChannelCheckResult Fail(string error, bool needsPluginDownload = false)
        => new(false, error, needsPluginDownload);
}


/// <summary>
/// B 站渠道的三条件校验。
/// </summary>
/// <remarks>
/// <para>
/// <b>这是最危险的失败模式</b>：插件缺失时游戏会<b>静默回落</b>到官服，
/// 用户以为切到了 B 站。所以启动前必须逐条校验。见 <c>docs/04-FEATURES.md</c> §2.5。
/// </para>
/// <para>
/// <see cref="Validate(GameChannel, GameDefinition, string)"/> 是<b>无副作用</b>的读操作：
/// 只读文件系统，不写任何东西，可被单元测试直接调用（U10）。
/// </para>
/// </remarks>
public static class ChannelValidator
{
    /// <summary>
    /// 校验某个渠道是否可以启动。
    /// </summary>
    /// <param name="channel">目标渠道。</param>
    /// <param name="definition">游戏定义（提供 edition / 数据目录）。</param>
    /// <param name="gameDirectory">游戏安装目录（<c>config.ini</c> 与插件都在这里）。</param>
    public static ChannelCheckResult Validate(GameChannel channel, GameDefinition definition, string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // ① 官方国服：没有额外前置条件
        if (channel != GameChannel.Bilibili)
        {
            if (!definition.Supports(channel))
            {
                return ChannelCheckResult.Fail(
                    $"{definition.DisplayName}（{definition.Edition}）不支持 {ChannelProfile.FromChannel(channel).DisplayName} 渠道。");
            }

            return ChannelCheckResult.Success();
        }

        // ② 条件 1：必须是国服（YuanShen.exe）—— 硬拒绝
        if (definition.Edition != GameEdition.China)
        {
            return ChannelCheckResult.Fail(
                $"Bilibili 渠道仅国服可用，当前是 {definition.Edition} 版（{definition.Executable}）。");
        }

        // ③ 游戏必须声明支持 B 站渠道（ZZZ 没有 B 站渠道）
        if (!definition.Supports(GameChannel.Bilibili))
        {
            return ChannelCheckResult.Fail(
                $"{definition.DisplayName}（{definition.Edition}）没有 Bilibili 渠道。");
        }

        // ④ 条件 2：YuanShen_Data/Plugins/PCGameSDK.dll 存在且非空
        var plugin = ValidatePlugin(definition, gameDirectory);
        if (!plugin.Ok)
        {
            return plugin;
        }

        // ⑤ 条件 3：config.ini 已正确写入（可修复，不算失败）
        var needsRewrite = !IsConfigChanneled(gameDirectory, ChannelProfile.Bilibili);

        return needsRewrite
            ? ChannelCheckResult.Success(
                needsConfigRewrite: true,
                warning: $"config.ini 尚未写入 Bilibili 渠道（channel=14, cps=bilibili, sub_channel=0），启动前会重写。")
            : ChannelCheckResult.Success();
    }


    /// <summary>
    /// 用已知的文档内容校验渠道（跳过文件读取，便于测试与"环境 shell"预览）。
    /// </summary>
    public static ChannelCheckResult Validate(
        GameChannel channel,
        GameDefinition definition,
        string gameDirectory,
        GameConfigIniFile? config)
    {
        var result = Validate(channel, definition, gameDirectory);
        if (!result.Ok || channel != GameChannel.Bilibili)
        {
            return result;
        }

        if (config is null || !IsChanneled(config, ChannelProfile.Bilibili))
        {
            return ChannelCheckResult.Success(
                needsConfigRewrite: true,
                warning: "config.ini 尚未写入 Bilibili 渠道（channel=14, cps=bilibili, sub_channel=0），启动前会重写。");
        }

        return ChannelCheckResult.Success();
    }


    /// <summary>只校验插件（条件 2）。</summary>
    public static ChannelCheckResult ValidatePlugin(GameDefinition definition, string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            return ChannelCheckResult.Fail("游戏目录为空，无法校验 Bilibili 插件。", needsPluginDownload: true);
        }

        var path = definition.GetBilibiliPluginPath(gameDirectory);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return ChannelCheckResult.Fail(
                    $"缺少 Bilibili 渠道插件：{path}（缺失时游戏会静默回落到官服）。",
                    needsPluginDownload: true);
            }

            if (info.Length == 0)
            {
                return ChannelCheckResult.Fail(
                    $"Bilibili 渠道插件是空文件：{path}（缺失时游戏会静默回落到官服）。",
                    needsPluginDownload: true);
            }
        }
        catch (Exception ex)
        {
            return ChannelCheckResult.Fail(
                $"无法读取 Bilibili 渠道插件 {path}：{ex.Message}",
                needsPluginDownload: true);
        }

        return ChannelCheckResult.Success();
    }


    /// <summary>磁盘上的 <c>config.ini</c> 是否已写成该渠道。</summary>
    public static bool IsConfigChanneled(string gameDirectory, ChannelProfile profile)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            return false;
        }

        var path = GameConfigIniFile.GetConfigPath(gameDirectory);
        if (!File.Exists(path))
        {
            return false;
        }

        return IsChanneled(GameConfigIniFile.Read(path), profile);
    }


    /// <summary>文档是否已写成该渠道。</summary>
    public static bool IsChanneled(GameConfigIniFile document, ChannelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(document);
        var general = document.General;
        return general.Channel == profile.ChannelId
               && general.SubChannel == profile.SubChannel
               && string.Equals(general.Cps ?? "", profile.Cps, StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// 把渠道写进 <c>config.ini</c>（原子写、保留未知键），
    /// 返回写入后的文档。<b>这是有副作用的辅助方法</b>；校验请用
    /// <see cref="Validate(GameChannel, GameDefinition, string)"/>。
    /// </summary>
    /// <param name="gameVersion">
    /// 游戏版本；<c>null</c> 时保留 <c>config.ini</c> 里已有的 <c>game_version</c>。
    /// </param>
    public static GameConfigIniFile ApplyChannel(
        GameDefinition definition,
        string gameDirectory,
        GameChannel channel,
        string? gameVersion = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);

        var profile = ChannelProfile.FromChannel(channel);
        var document = GameConfigIniFile.ReadFromGameDirectory(gameDirectory);

        // 首次为某个渠道建 config.ini 时字段集合与游戏原生格式一致
        if (document.PresentKeys.Count == 0 && document.ExtraKeys.Count == 0)
        {
            document = GameConfigIniFile.CreateEmpty();
        }

        // 注意：不主动补 game_biz / sdk_version 等扩展键——新建出来的文件必须与
        // 游戏原生格式逐字节一致（U1）。文件里原本就有的键原样保留。
        var updated = document.WithChannel(profile, gameVersion ?? document.General.GameVersion?.ToString());
        updated.WriteToGameDirectory(gameDirectory);
        return updated;
    }


    /// <summary>读磁盘上的 <c>config.ini</c>，返回它表示的渠道（无文件/无法识别 → <c>null</c>）。</summary>
    public static ChannelProfile? DetectChannel(string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            return null;
        }

        var path = GameConfigIniFile.GetConfigPath(gameDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        return GameConfigIniFile.Read(path).GetChannelProfile();
    }
}
