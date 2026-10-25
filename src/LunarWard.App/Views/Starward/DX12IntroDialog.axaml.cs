using LunarWard.App.Localization;
using LunarWard.App.Views.Starward.Controls;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// Starward <c>DX12IntroDialog</c> 的等价物（启动器 DX12 开关旁的「详情」）。
/// </summary>
/// <remarks>
/// 文案直接按当前语言给字面量，避免为一次性弹窗再往两份字典里插键（前几轮手插字典出过错）。
/// 上游是窗口内 <c>ContentDialog</c>，这里继承 <see cref="StarwardContentDialog"/> 后同样浮在主窗口里；
/// 关闭走容器的 X / 「关闭」按钮 / ESC。
/// </remarks>
public partial class DX12IntroDialog : StarwardContentDialog
{
    public DX12IntroDialog()
    {
        InitializeComponent();
        bool zh = AppStrings.Instance.Language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        Heading = zh ? "DX12 效果详情" : "DX12 effect details";
        RegularGraphics = zh ? "常规画质" : "Regular graphics";
        EnhancedGraphics = zh ? "DX12 增强画质" : "DX12 enhanced graphics";
        Description = zh
            ? "开启后游戏将以 DirectX 12 渲染，支持更完整的光追与更稳定的帧时间；关闭后回到 DirectX 11。切换会重新进入游戏才生效。"
            : "When enabled the game renders with DirectX 12, giving fuller ray tracing and steadier frame times; disabling returns to DirectX 11. The change takes effect after relaunching the game.";
        CloseButtonText = AppStrings.Instance["Common.Close"];
        DataContext = this;
    }

    public string Heading { get; }

    public string RegularGraphics { get; }

    public string EnhancedGraphics { get; }

    public string Description { get; }
}
