using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using System;
using System.Diagnostics;

namespace LunarWard.Controls;


/// <summary>
/// 上游 <c>Starward.Controls.ColorfulTextBlock</c> 的 Avalonia 移植：把游戏公告里的内联标记
/// （<c>\n</c>、<c>&lt;cAARRGGBB&gt;…&lt;</c>、<c>&lt;i&gt;…&lt;</c>）渲染成富文本。
/// </summary>
/// <remarks>
/// 差异（docs/09）：WinUI 用 DependencyProperty + <c>[ObservableProperty]</c> 的 partial 属性，
/// Avalonia 这边统一用 <see cref="StyledProperty{T}"/>（AvaloniaObject 已实现 INotifyPropertyChanged，
/// 不能再挂 <c>[INotifyPropertyChanged]</c>，MVVMTK0001）；内联透传从 XAML 的 x:Bind 挪到构造函数里的 Bind。
/// 解析逻辑与上游逐行一致。
/// </remarks>
public sealed partial class ColorfulTextBlock : UserControl
{


    public ColorfulTextBlock()
    {
        InitializeComponent();
        // 上游 XAML：HorizontalAlignment="{x:Bind HorizontalAlignment}" … TextWrapping="{x:Bind TextWrapping}"
        ThisTextBlock.Bind(HorizontalAlignmentProperty, this.GetObservable(HorizontalAlignmentProperty));
        ThisTextBlock.Bind(VerticalAlignmentProperty, this.GetObservable(VerticalAlignmentProperty));
        ThisTextBlock.Bind(FontFamilyProperty, this.GetObservable(FontFamilyProperty));
        ThisTextBlock.Bind(FontSizeProperty, this.GetObservable(FontSizeProperty));
        ThisTextBlock.Bind(FontWeightProperty, this.GetObservable(FontWeightProperty));
        ThisTextBlock.Bind(ForegroundProperty, this.GetObservable(ForegroundProperty));
        ThisTextBlock.Bind(TextBlock.TextWrappingProperty, this.GetObservable(TextWrappingProperty));
    }




    public TextWrapping TextWrapping
    {
        get => GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    // Using a StyledProperty as the backing store for TextWrapping.  This enables animation, styling, binding, etc...
    public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
        AvaloniaProperty.Register<ColorfulTextBlock, TextWrapping>(nameof(TextWrapping));



    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ColorfulTextBlock, string>(nameof(Text), string.Empty);


    static ColorfulTextBlock()
    {
        TextProperty.Changed.AddClassHandler<ColorfulTextBlock>((control, e) => control.OnTextChanged(e.NewValue as string ?? string.Empty));
    }



    private void OnTextChanged(string value)
    {
        try
        {
            var text = ThisTextBlock;
            var inlines = text.Inlines ??= new InlineCollection();
            inlines.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            var desc = value.AsSpan();
            int lastIndex = 0;
            for (int i = 0; i < desc.Length; i++)
            {
                // 换行
                if (desc[i] == '\\' && desc[i + 1] == 'n')
                {
                    inlines.Add(new Run { Text = desc[lastIndex..i].ToString() });
                    inlines.Add(new LineBreak());
                    i += 1;
                    lastIndex = i + 1;
                }
                // 颜色
                if (desc[i] == '<' && desc[i + 1] == 'c')
                {
                    inlines.Add(new Run { Text = desc[lastIndex..i].ToString() });
                    var colorLength = desc.Slice(i + 8).IndexOf('>');
                    var colorString = desc.Slice(i + 8, colorLength);
                    var color = Convert.FromHexString(colorString);
                    var textLength = desc.Slice(i + 9 + colorLength).IndexOf('<');
                    if (colorLength == 8)
                    {
                        inlines.Add(new Run
                        {
                            Text = desc.Slice(i + 9 + colorLength, textLength).ToString(),
                            Foreground = new SolidColorBrush(Color.FromArgb(color[3], color[0], color[1], color[2])),
                        });
                    }
                    else if (colorLength == 6)
                    {
                        inlines.Add(new Run
                        {
                            Text = desc.Slice(i + 9 + colorLength, textLength).ToString(),
                            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, color[0], color[1], color[2])),
                        });
                    }
                    else
                    {
                        inlines.Add(new Run
                        {
                            Text = desc.Slice(i + 9 + colorLength, textLength).ToString(),
                        });
                    }
                    i += 16 + colorLength + textLength;
                    lastIndex = i + 1;
                }
                // 引用
                if (desc[i] == '<' && desc[i + 1] == 'i')
                {
                    inlines.Add(new Run { Text = desc[lastIndex..i].ToString() });
                    var length = desc.Slice(i + 3).IndexOf('<');
                    inlines.Add(new Run
                    {
                        Text = desc.Slice(i + 3, length).ToString(),
                        FontStyle = FontStyle.Italic,
                    });
                    i += length + 6;
                    lastIndex = i + 1;
                }
                // 结尾
                if (i == desc.Length - 1)
                {
                    inlines.Add(new Run { Text = desc.Slice(lastIndex).ToString() });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            if (ThisTextBlock.Inlines is { } inlines)
            {
                inlines.Clear();
            }
            ThisTextBlock.Text = value;
        }
    }




}
