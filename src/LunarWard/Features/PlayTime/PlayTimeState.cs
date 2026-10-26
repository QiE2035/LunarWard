namespace LunarWard.Features.PlayTime;

/// <summary>
/// 游戏时长的记录状态，与上游 <c>Starward.Features.PlayTime.PlayTimeState</c> 逐值一致。
/// </summary>
public enum PlayTimeState
{

    None = 0,

    Start = 1,

    Play = 2,

    Stop = 3,

    Error = 4,

}
