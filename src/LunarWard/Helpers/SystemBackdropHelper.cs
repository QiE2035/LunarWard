using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Serilog;

namespace LunarWard.Helpers;

/// <summary>
/// 窗口背景材质（对齐上游 <c>Starward/Helpers/SystemBackdropHelper.cs</c>，352 行）。
/// <para>
/// 上游用 Windows Composition 的 <c>MicaController</c> / <c>DesktopAcrylicController</c>：
/// 可以真正取到桌面模糊的材质，并可调 Tint / Luminosity / Fallback 四组颜色。
/// Linux 上没有等价的系统材质 API，这里按能力降级为「合成器模糊」：
/// </para>
/// <list type="bullet">
///   <item>合成器支持 <see cref="WindowTransparencyLevel.Blur"/>（KWin/GNOME/Mutter 的 blur 协议）时，
///         用 <c>TransparencyLevelHint = [Blur]</c> + 半透明 Tint 背景近似 Acrylic；</item>
///   <item>不支持时退到 <see cref="WindowTransparencyLevel.Transparent"/> + Tint 背景；</item>
///   <item>连透明都不支持时用 FallbackColor 纯色（不静默黑屏）。</item>
/// </list>
/// <para>
/// 返回值语义与上游一致：是否真的装上了材质。Mica 永远返回 <c>false</c>（Linux 无此概念），
/// Acrylic 在拿到合成器模糊时返回 <c>true</c>；调用方按返回值决定是否再衬托底色。
/// </para>
/// </summary>
public class SystemBackdropHelper
{

    private readonly Window _window;

    private SystemBackdropProperty? backdropProperty;

    private bool applied;



    public SystemBackdropHelper(Window window, SystemBackdropProperty? backdropProperty = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        this.backdropProperty = backdropProperty;
    }



    /// <summary>还原窗口本身的外观，不再持有任何材质。</summary>
    public void ResetBackdrop()
    {
        if (!applied)
        {
            return;
        }
        _window.TransparencyLevelHint = [WindowTransparencyLevel.None];
        _window.Background = null;
        applied = false;
    }



    public void SetBackdropProperty(SystemBackdropProperty? backdropProperty = null)
    {
        this.backdropProperty = backdropProperty;
        if (applied)
        {
            ApplyProperties();
        }
    }



    /// <summary>
    /// 上游的 Mica 材质。Linux 无系统材质 API，这里只按 Acrylic 的路径降级一次，
    /// 返回值恒为 <c>false</c> 以如实反映「Mica 不可用」。
    /// </summary>
    public bool TrySetMica(bool useMicaAlt = false, bool fallbackToAcrylic = false, bool alwaysActive = false)
    {
        ResetBackdrop();
        backdropProperty ??= useMicaAlt ? SystemBackdropProperty.MicaAltDefault : SystemBackdropProperty.MicaDefault;
        ApplyProperties();
        Log.Information("Mica backdrop is not available on Linux; degraded to compositor blur / transparent fallback.");
        return false;
    }



    /// <summary>
    /// Acrylic 材质的等价实现：合成器 blur + Tint 半透明层。
    /// </summary>
    public bool TrySetAcrylic(bool alwaysActive = false)
    {
        ResetBackdrop();
        backdropProperty ??= SystemBackdropProperty.AcrylicDefault;
        ApplyProperties();
        return _window.ActualTransparencyLevel is WindowTransparencyLevel.Blur or WindowTransparencyLevel.Transparent;
    }



    private void ApplyProperties()
    {
        var property = backdropProperty ?? SystemBackdropProperty.AcrylicDefault;
        var dark = _window.ActualThemeVariant == ThemeVariant.Dark;

        var tint = Color.FromUInt32(dark ? property.TintColorDark : property.TintColorLight);
        var tintOpacity = dark ? property.TintOpacityDark : property.TintOpacityLight;
        var fallback = Color.FromUInt32(dark ? property.FallbackColorDark : property.FallbackColorLight);
        var luminosity = dark ? property.LuminosityOpacityDark : property.LuminosityOpacityLight;

        // Avalonia 的 Hint 是「期望值」，由平台挑一个能用的；先试 blur，再退 transparent/opaque。
        _window.TransparencyLevelHint = [WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None];

        var effectiveOpacity = Math.Clamp(tintOpacity > 0 ? tintOpacity : 1 - luminosity, 0, 1);
        _window.Background = new SolidColorBrush(tint, _window.ActualTransparencyLevel is WindowTransparencyLevel.None
            ? 1
            : effectiveOpacity);

        if (_window.ActualTransparencyLevel is WindowTransparencyLevel.None)
        {
            // An opaque compositor: show the fallback color instead of the tint (same as upstream's FallbackColor).
            _window.Background = new SolidColorBrush(fallback);
        }

        applied = true;
    }


}



/// <summary>对齐上游 <c>SystemBackdropProperty</c>（record + required init 属性 + 三套预设）。</summary>
public record SystemBackdropProperty
{

    public required uint FallbackColorDark { get; init; }

    public required uint FallbackColorLight { get; init; }

    public required float LuminosityOpacityDark { get; init; }

    public required float LuminosityOpacityLight { get; init; }

    public required uint TintColorDark { get; init; }

    public required uint TintColorLight { get; init; }

    public required float TintOpacityDark { get; init; }

    public required float TintOpacityLight { get; init; }


    public static readonly SystemBackdropProperty AcrylicDefault = new()
    {
        FallbackColorDark = 0xFF545454,
        FallbackColorLight = 0xFFD3D3D3,
        LuminosityOpacityDark = 0.64f,
        LuminosityOpacityLight = 0.64f,
        TintColorDark = 0xFF545454,
        TintColorLight = 0xFFD3D3D3,
        TintOpacityDark = 0,
        TintOpacityLight = 0,
    };

    public static readonly SystemBackdropProperty MicaDefault = new()
    {
        FallbackColorDark = 0xFF202020,
        FallbackColorLight = 0xFFF3F3F3,
        LuminosityOpacityDark = 1,
        LuminosityOpacityLight = 1,
        TintColorDark = 0xFF202020,
        TintColorLight = 0xFFF3F3F3,
        TintOpacityDark = 0.8f,
        TintOpacityLight = 0.5f,
    };

    public static readonly SystemBackdropProperty MicaAltDefault = new()
    {
        FallbackColorDark = 0xFF202020,
        FallbackColorLight = 0xFFE8E8E8,
        LuminosityOpacityDark = 1,
        LuminosityOpacityLight = 1,
        TintColorDark = 0xFF0A0A0A,
        TintColorLight = 0xFFDADADA,
        TintOpacityDark = 0,
        TintOpacityLight = 0.5f,
    };

}
