using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LunarWard.Frameworks;

/// <summary>
/// <c>Microsoft.UI.Xaml.Controls.FontIcon</c> 的等价实现：字体图标就是一段带图标字体的文本。
/// 上游用 <c>&lt;FontIcon Glyph="&#xE80F;"/&gt;</c>，本仓库沿用同样的 <see cref="Glyph"/> 写法，
/// 渲染为 <see cref="TextBlock"/>（字体取全局图标字体，见 Styles/Starward.axaml 的 <c>StarIconFontFamily</c>）。
/// </summary>
public class FontIcon : TextBlock
{

    public static readonly StyledProperty<string?> GlyphProperty =
        AvaloniaProperty.Register<FontIcon, string?>(nameof(Glyph));


    public FontIcon()
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        FontSize = 16;
        if (Application.Current?.TryGetResource("StarIconFontFamily", null, out object? font) is true && font is FontFamily family)
        {
            FontFamily = family;
        }
    }


    public string? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphProperty)
        {
            Text = Glyph;
        }
    }

}
