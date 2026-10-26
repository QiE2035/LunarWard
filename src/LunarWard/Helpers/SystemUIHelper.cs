using System;

namespace LunarWard.Helpers;


/// <summary>
/// 不可用状态，系统设置远比以下内容更复杂。
/// 上游：src/Starward/Helpers/SystemUIHelper.cs（User32.SystemParametersInfo 读写
/// SPI_GETDISABLEOVERLAPPEDCONTENT / SPI_GETCLIENTAREAANIMATION）。
/// 平台替换：XDG 桌面没有等价的「透明效果 / 动画效果」全局查询接口；这里保留成员形状，
/// 值恒为 true，且与上游一样标记为不可用（Obsolete 第二参数 = true，禁止调用）。
/// </summary>
[Obsolete("不可用状态", true)]
public static class SystemUIHelper
{


    /// <summary>
    /// 辅助功能 - 视觉效果 - 透明效果
    /// </summary>
    public static bool TransparencyEffectEnabled
    {
        get => true;
        set { }
    }



    /// <summary>
    /// 辅助功能 - 视觉效果 - 动画效果
    /// </summary>
    public static bool AnimationEffectEnabled
    {
        get => true;
        set { }
    }



}
