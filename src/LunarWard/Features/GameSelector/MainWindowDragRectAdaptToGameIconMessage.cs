namespace LunarWard.Features.GameSelector;

/// <summary>
/// 主窗口可拖动矩形适应游戏图标。<b>原样搬自</b> <c>Starward/src/Starward/Features/GameSelector/MainWindowDragRectAdaptToGameIconMessage.cs</c>。
/// </summary>
internal class MainWindowDragRectAdaptToGameIconMessage
{

    public bool IgnoreDpiChanged { get; set; }


    public MainWindowDragRectAdaptToGameIconMessage(bool ignoreDpiChanged = false)
    {
        IgnoreDpiChanged = ignoreDpiChanged;
    }

}
