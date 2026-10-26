using LunarWard.Core.HoYoPlay;

namespace LunarWard.Features.Background;

/// <summary>
/// 背景切换消息（对应上游 <c>Starward/Features/Background/BackgroundChangedMessage.cs</c>）。
/// </summary>
internal class BackgroundChangedMessage
{

    public GameBackground? GameBackground { get; set; }


    public BackgroundChangedMessage(GameBackground? gameBackground = null)
    {
        GameBackground = gameBackground;
    }

}
