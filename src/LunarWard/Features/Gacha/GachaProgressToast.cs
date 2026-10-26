using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LunarWard.Helpers;
using LunarWard.Language;
using System;
using System.Threading;

namespace LunarWard.Features.Gacha;

/// <summary>
/// 上游 <c>InfoBar</c>（消息 + 进度文案 + 取消按钮）的等价物。
/// </summary>
/// <remarks>
/// 上游抽卡页用 <c>new InfoBar { Severity, Background = "CustomAcrylicBrush", ActionButton = button }</c>
/// 加进 <c>InAppToast.MainWindow</c>，并通过 <c>infoBar.Message = str</c> 实时刷新进度。
/// Avalonia 没有 <c>InfoBar</c>，<see cref="ToastItem"/> 的 <c>Message</c> 又是 init-only 无法刷新，
/// 因此这里补一个可更新标题/消息/取消按钮的轻提示，复用 <see cref="InAppToast"/> 的容器。
/// 见 docs/09 的抽卡簇平台替换点。
/// </remarks>
internal sealed class GachaProgressToast : Border
{

    private readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, IsVisible = false };

    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };

    private readonly Button _button;


    public event EventHandler? CancelClicked;


    public string? Title
    {
        get => _title.Text;
        set
        {
            _title.Text = value;
            _title.IsVisible = !string.IsNullOrWhiteSpace(value);
        }
    }


    public string? Message
    {
        get => _message.Text;
        set => _message.Text = value;
    }


    public GachaProgressToast(string cancelText)
    {
        Padding = new Thickness(12, 8);
        CornerRadius = new CornerRadius(4);
        Background = new SolidColorBrush(Color.Parse("#F52C2C2C"));
        MinWidth = 280;
        MaxWidth = 420;
        _button = new Button { Content = cancelText, HorizontalAlignment = HorizontalAlignment.Right };
        _button.Click += (_, _) => CancelClicked?.Invoke(this, EventArgs.Empty);
        Child = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                _title,
                _message,
                _button,
            },
        };
    }


    public void HideCancelButton() => _button.IsVisible = false;



    /// <summary>
    /// 对应上游 <c>new InfoBar { ... }</c> 加进 <see cref="InAppToast.MainWindow"/> 的那一段。
    /// </summary>
    public static GachaProgressToast? Show(CancellationTokenSource cancelSource)
    {
        if (InAppToast.MainWindow is not { } host)
        {
            return null;
        }
        var toast = new GachaProgressToast(Lang.Common_Cancel);
        toast.CancelClicked += (_, _) =>
        {
            cancelSource.Cancel();
            // 操作已取消
            toast.Message = Lang.GachaLogPage_OperationCanceled;
            toast.HideCancelButton();
        };
        host.Children.Add(toast);
        return toast;
    }


    public static void Close(GachaProgressToast? toast, string? title, InfoBarSeverity severity)
    {
        if (toast is null)
        {
            if (severity == InfoBarSeverity.Success)
            {
                InAppToast.MainWindow?.Success(title);
            }
            return;
        }
        toast.Title = title;
        toast.Message = null;
        toast.HideCancelButton();
        DispatcherTimer.RunOnce(() =>
        {
            if (InAppToast.MainWindow is { } host && host.Children.Contains(toast))
            {
                host.Children.Remove(toast);
            }
        }, TimeSpan.FromSeconds(5));
    }

}
