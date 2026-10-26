using LunarWard.Core;
using System;

namespace LunarWard.Features.PlayTime;


/// <summary>
/// 一条游戏时长记录（对应数据库表 <c>PlayTimeItem</c>）。
/// 对应上游 <c>Starward.Features.PlayTime.PlayTimeItem</c>。
/// </summary>
public class PlayTimeItem
{
    public long TimeStamp { get; set; }

    public GameBiz GameBiz { get; set; }

    public int Pid { get; set; }

    public PlayTimeState State { get; set; }

    public long CursorPos { get; set; }

    public string? Message { get; set; }
}


/// <summary>
/// 聚合统计时只需要的最小列集合（不含 CursorPos/Message）。
/// </summary>
internal struct PlayTimeItemStruct
{
    public long TimeStamp { get; set; }

    public GameBiz GameBiz { get; set; }

    public int Pid { get; set; }

    public PlayTimeState State { get; set; }
}


/// <summary>
/// 一次游戏会话（对应数据库表 <c>PlayTimeStats</c>）。
/// </summary>
public class PlayTimeStats
{
    public GameBiz GameBiz { get; set; }

    public int Pid { get; set; }

    public long StartTime { get; set; }

    public long EndTime { get; set; }

    public bool Interruption { get; set; }

    public int Type { get; set; }
}


/// <summary>
/// 按天聚合的游戏时长。
/// </summary>
public class PlayTimeDayItem
{
    public DateTimeOffset Date { get; set; }

    public TimeSpan PlayTime { get; set; }
}
