namespace LunarWard.Features.Background;

/// <summary>
/// 视频背景音量变更消息（对应上游 <c>Starward/Features/Background/VideoBgVolumeChangedMessage.cs</c>）。
/// </summary>
internal class VideoBgVolumeChangedMessage
{

    public int Volume { get; set; }


    public VideoBgVolumeChangedMessage(int volume)
    {
        Volume = volume;
    }

}
