using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace LunarWard.Services;

/// <summary>提示条严重级别（对应上游 InfoBar 的 Informational / Success / Warning / Error）。</summary>
public enum ToastSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}


/// <summary>
/// 应用内提示条（复刻 Starward <c>Helpers/InAppToast.cs</c> 的可视行为）。
/// </summary>
/// <remarks>
/// <para>
/// 上游是 WinUI 的 <c>Behavior&lt;StackPanel&gt;</c> + <c>InfoBar</c>：把 InfoBar 追加到
/// 指定的 StackPanel，30 秒后自动移除已关闭的项。Avalonia 没有 InfoBar / Behavior，
/// 这里改成「静态宿主 + 自绘 Border」，在 `StarwardShell` 的
/// 右下角 StackPanel 里堆叠，语义与位置对应上游 MainWindow 底部的 RootInfoBar。
/// </para>
/// <para>
/// 只允许在主线程调用（内部用 <see cref="Dispatcher.UIThread.Post"/> 兜底）。
/// </para>
/// </remarks>
public static class InAppToast
{
    private static Panel? _host;


    /// <summary>绑定提示条宿主（外壳窗口初始化时调用一次）。</summary>
    public static void Initialize(Panel host) => _host = host;


    public static void Show(string title, string? message = null, ToastSeverity severity = ToastSeverity.Informational)
    {
        Dispatcher.UIThread.Post(() => ShowCore(title, message, severity));
    }


    public static void Success(string title, string? message = null) => Show(title, message, ToastSeverity.Success);

    public static void Warning(string title, string? message = null) => Show(title, message, ToastSeverity.Warning);

    public static void Error(string title, string? message = null) => Show(title, message, ToastSeverity.Error);


    /// <summary>把异常做成一条 Error 提示（对应上游 <c>InAppToast.MainWindow?.Error(ex)</c>）。</summary>
    public static void Error(Exception ex) => Error(ex.GetType().Name, ex.Message);


    private static void ShowCore(string title, string? message, ToastSeverity severity)
    {
        if (_host is null)
        {
            return;
        }

        Color accent = severity switch
        {
            ToastSeverity.Success => Color.Parse("#00E079"),
            ToastSeverity.Warning => Color.Parse("#F5A623"),
            ToastSeverity.Error => Color.Parse("#C83C23"),
            _ => Color.Parse("#4A9EFF"),
        };

        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
        });

        if (!string.IsNullOrWhiteSpace(message))
        {
            content.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.Parse("#B0FFFFFF")),
            });
        }

        var bar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#E6202020")),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 0, 0, 8),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 8,
                Blur = 24,
                Color = Color.Parse("#80000000"),
            }),
        };

        // 左侧 3px 级别色条
        var accentBar = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(accent),
            Margin = new Thickness(-12, 0, 12, 0),
        };
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(accentBar, 0);
        Grid.SetColumn(content, 1);
        layout.Children.Add(accentBar);
        layout.Children.Add(content);
        bar.Child = layout;

        _host.Children.Insert(0, bar);

        // 上游定时器 30s 清理；这里单条也 30s 后淡出移除
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _host.Children.Remove(bar);
        };
        timer.Start();
    }
}
