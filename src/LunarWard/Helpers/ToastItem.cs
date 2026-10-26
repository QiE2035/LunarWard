using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace LunarWard.Helpers;

/// <summary>
/// 上游 WinUI <c>InfoBar</c> 的等价轻提示项，供 <see cref="InAppToast"/> 使用。
/// </summary>
internal class ToastItem : Border
{

    private readonly TextBlock _title;
    private readonly TextBlock _message;
    private readonly Button _button;
    private readonly Button _closeButton;
    private bool _isOpen;


    public InfoBarSeverity Severity { get; init; }

    public string? Title
    {
        get => _title.Text;
        init => _title.Text = value;
    }

    public string? Message
    {
        get => _message.Text;
        init
        {
            _message.Text = value;
            _message.IsVisible = !string.IsNullOrWhiteSpace(value);
        }
    }

    public string? ButtonContent
    {
        get => _button.Content as string;
        init
        {
            _button.Content = value;
            _button.IsVisible = !string.IsNullOrWhiteSpace(value) && ButtonAction is not null;
        }
    }

    public Action? ButtonAction { get; init; }

    public Action? ClosedAction { get; init; }


    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            _isOpen = value;
            if (!value)
            {
                ClosedAction?.Invoke();
            }
            IsVisible = value;
        }
    }


    public ToastItem()
    {
        _title = new TextBlock { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        _message = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.9, IsVisible = false };
        _button = new Button { IsVisible = false, Margin = new Thickness(8, 0, 0, 0) };
        _closeButton = new Button { Content = "✕", Padding = new Thickness(6, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };

        _button.Click += (_, _) =>
        {
            try
            {
                ButtonAction?.Invoke();
            }
            catch
            {
            }
            IsOpen = false;
        };
        _closeButton.Click += (_, _) => IsOpen = false;

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Children =
            {
                new StackPanel { Orientation = Orientation.Vertical, Children = { _title, _message } },
            },
        };
        Grid.SetColumn(_button, 1);
        Grid.SetColumn(_closeButton, 2);
        content.Children.Add(_button);
        content.Children.Add(_closeButton);

        Child = content;
        Padding = new Thickness(12, 8);
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        MinWidth = 280;
        MaxWidth = 420;
        Margin = new Thickness(0, 4, 12, 0);
        Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x2B, 0x2B, 0x2B));
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
    }


    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplySeverity();
    }


    private void ApplySeverity()
    {
        Color accent = Severity switch
        {
            InfoBarSeverity.Success => Color.FromRgb(0x6C, 0xCB, 0x5F),
            InfoBarSeverity.Warning => Color.FromRgb(0xFF, 0xC8, 0x3D),
            InfoBarSeverity.Error => Color.FromRgb(0xFF, 0x99, 0xA4),
            _ => Color.FromRgb(0x60, 0xCD, 0xFF),
        };
        BorderBrush = new SolidColorBrush(accent);
        _title.Foreground = new SolidColorBrush(accent);
        _message.Foreground = Brushes.White;
        _closeButton.Foreground = Brushes.White;
    }

}
