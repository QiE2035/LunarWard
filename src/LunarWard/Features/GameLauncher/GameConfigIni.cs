using System;
using LunarWard.Core;
using Microsoft.Extensions.Configuration;

namespace LunarWard.Features.GameLauncher;

/// <summary>
/// <b>原样移植自</b> <c>Starward/src/Starward/Features/GameLauncher/GameConfigIni.cs</c>。
/// 游戏目录下 <c>config.ini</c> 的 <c>[General]</c> 段。
/// </summary>
/// <remarks>
/// 上游文件本来就在 <c>Features/GameLauncher</c>，这里按上游落位；同名实现已随 <c>LunarWard.Games</c>
/// 工程折叠一并删除（task-18），现在是唯一实现。
/// </remarks>
public class GameConfigIni
{

    [ConfigurationKeyName("channel")]
    public int Channel { get; set; }


    [ConfigurationKeyName("sub_channel")]
    public int SubChannel { get; set; }


    [ConfigurationKeyName("cps")]
    public string Cps { get; set; } = default!;


    [ConfigurationKeyName("game_version")]
    public Version? GameVersion { get; set; }


    [ConfigurationKeyName("sdk_version")]
    public Version? SdkVersion { get; set; }


    [ConfigurationKeyName("game_biz")]
    public GameBiz GameBiz { get; set; }


    [ConfigurationKeyName("uapc")]
    public string Uapc { get; set; } = default!;


    [ConfigurationKeyName("downloading_mode")]
    public string DownloadingMode { get; set; } = default!;

}
