using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace LunarWard.Helpers;

/// <summary>上游 <c>Microsoft.UI.Xaml.Controls.InfoBarSeverity</c> 的等价枚举（Avalonia 无 InfoBar）。</summary>
public enum InfoBarSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}

/// <summary>
/// 上游 <c>Starward.Helpers.InAppToast</c> 的 Avalonia 等价实现。
/// </summary>
/// <remarks>
/// <para>
/// 上游是一个 <c>Microsoft.Xaml.Interactivity.Behavior&lt;StackPanel&gt;</c>，挂在 MainWindow 的
/// 通知区 StackPanel 上，用 WinUI <c>InfoBar</c> 展示轻提示。Avalonia 没有 InfoBar，也没有内置的
/// Behavior 基础设施（上游依赖 <c>Microsoft.Xaml.Interactivity</c>，本项目不引入 Avalonia 版三方包），
/// 因此这里把 InAppToast 本身做成一个 <see cref="StackPanel"/>：XAML 里
/// <c>&lt;helpers:InAppToast Tag="MainWindow"/&gt;</c> 直接放进通知区即可，公开方法
/// （<see cref="Information"/> / <see cref="Success"/> / <see cref="Warning"/> / <see cref="Error"/> /
/// <see cref="ShowWithButton"/>）与上游同名同参。
/// </para>
/// <para>差异登记 docs/09-LINUX-DIVERGENCES.md。</para>
/// </remarks>
public class InAppToast : StackPanel
{

    private static readonly TimeSpan DismissInterval = TimeSpan.FromSeconds(1);


    private DispatcherTimer? _dismissTimer;


    public string? Tag { get; set; }


    /// <summary>上游 <c>InAppToast.MainWindow</c>：主窗口通知区的实例。</summary>
    public static InAppToast? MainWindow { get; private set; }


    public InAppToast()
    {
        Orientation = Orientation.Vertical;
        Spacing = 4;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        IsHitTestVisible = true;
        AttachedToVisualTree += (_, _) =>
        {
            if (Tag is nameof(MainWindow))
            {
                MainWindow = this;
            }
            StartDismissTimer();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (Tag is nameof(MainWindow) && ReferenceEquals(MainWindow, this))
            {
                MainWindow = null;
            }
            StopDismissTimer();
        };
    }


    private void StartDismissTimer()
    {
        if (_dismissTimer is not null)
        {
            return;
        }
        _dismissTimer = new DispatcherTimer { Interval = DismissInterval };
        _dismissTimer.Tick += (_, _) => RemoveClosedItems();
        _dismissTimer.Start();
    }


    private void StopDismissTimer()
    {
        _dismissTimer?.Stop();
        _dismissTimer = null;
    }


    private void RemoveClosedItems()
    {
        try
        {
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i] is ToastItem { IsOpen: false })
                {
                    Children.RemoveAt(i);
                }
            }
        }
        catch
        {
        }
    }


    public void Information(string? title, string? message = null, int duration = 3000)
    {
        AddToast(InfoBarSeverity.Informational, title, message, duration);
    }


    public void Success(string? title, string? message = null, int duration = 3000)
    {
        AddToast(InfoBarSeverity.Success, title, message, duration);
    }


    public void Warning(string? title, string? message = null, int duration = 5000)
    {
        AddToast(InfoBarSeverity.Warning, title, message, duration);
    }


    public void Error(string? title, string? message = null, int duration = 5000)
    {
        AddToast(InfoBarSeverity.Error, title, message, duration);
    }


    public void Error(Exception ex, string? message = null, int duration = 5000)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            AddToast(InfoBarSeverity.Error, ex.GetType().Name, ex.Message, duration);
        }
        else
        {
            AddToast(InfoBarSeverity.Error, $"{ex.GetType().Name} - {message}", ex.Message, duration);
        }
    }


    public void ShowWithButton(InfoBarSeverity severity, string? title, string? message, string buttonContent, Action buttonAction, Action? closedAction = null, int duration = 0)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = Create(severity, title, message, buttonContent, buttonAction, closedAction);
            Show(item, duration);
        });
    }


    private void AddToast(InfoBarSeverity severity, string? title, string? message, int duration)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = Create(severity, title, message);
            Show(item, duration);
        });
    }


    private ToastItem Create(InfoBarSeverity severity, string? title, string? message, string? buttonContent = null, Action? buttonAction = null, Action? closedAction = null)
    {
        var item = new ToastItem
        {
            Severity = severity,
            Title = title,
            Message = message,
            ButtonContent = buttonContent,
            ButtonAction = buttonAction,
            ClosedAction = closedAction,
        };
        return item;
    }


    private async void Show(ToastItem item, int duration)
    {
        try
        {
            if (Children.Count >= 5)
            {
                Children.RemoveAt(0);
            }
            Children.Add(item);
            item.IsOpen = true;
            if (duration > 0)
            {
                await Task.Delay(duration);
                item.IsOpen = false;
            }
        }
        catch
        {
        }
    }

}
