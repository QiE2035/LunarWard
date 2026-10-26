using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using DialogHostAvalonia;
using LunarWard.Frameworks.Dialogs;

namespace LunarWard.Services;

/// <summary>
/// Starward ContentDialog 的统一入口：把内容挂到主窗口的 <c>DialogHost</c> 浮层上，并负责
/// 防重入、ESC / X / 「关闭」按钮的收口、焦点进入与归还。
/// </summary>
/// <remarks>
/// <para>
/// 用法（页面里）：
/// <code>
/// var dialog = new InstallGameDialog(context.Services, gameBiz, launcherId, installPath);
/// await DialogService.ShowAsync(dialog, this);
/// if (dialog.Request is { } request) { await BeginInstallAsync(request); }
/// </code>
/// 对应上游 <c>ContentDialog.ShowAsync()</c> + 读 dialog 属性。宿主不传时回退到主窗口
/// （<see cref="MainWindow.ContentDialogHost"/>），所以从任意页面/服务里都能弹。
/// </para>
/// <para>
/// 关闭只有一个出口：dialog 的 <see cref="StarwardContentDialog.CloseRequested"/>。ESC、右上角 X、
/// 底部「关闭」按钮、以及内容里业务按钮调 <c>Close()</c> 都触发它，这里统一调
/// <c>DialogSession.Close(null)</c>，再从 <c>Show</c> 返回后触发 <see cref="StarwardContentDialog.Closed"/>。
/// 因此调用方的 <c>await</c> 语义与上游一致：await 返回即对话框已关，读 <c>Request</c>/<c>Result</c> 有效。
/// </para>
/// <para>
/// 防重入：同一时刻只允许一个对话框（对应上游「一次只有一个 ContentDialog」的交互约束）。
/// 已在显示时第二次请求会被忽略并打印 <c>DIALOG_IGNORED</c>，不会抛异常打断调用方。
/// </para>
/// </remarks>
public static class DialogService
{
    private static readonly DialogReentrancyGate Gate = new();


    /// <summary>当前是否有对话框在显示（自检、防重入判断用）。</summary>
    public static bool IsDialogOpen => Gate.IsOpen;


    /// <summary>在主窗口浮层里显示 <paramref name="dialog"/>，等它关闭后返回。</summary>
    /// <param name="dialog">要显示的内容对话框。</param>
    /// <param name="host">宿主控件（通常是页面自身，如 <c>this</c>）；为 null 时用主窗口的 DialogHost。</param>
    public static async Task ShowAsync(StarwardContentDialog dialog, Control? host = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (ResolveHost(host) is not { } dialogHost)
        {
            Console.Error.WriteLine($"DIALOG_NO_HOST {dialog.GetType().Name}");
            return;
        }

        if (!Gate.TryEnter())
        {
            // 第二次请求（例如连点齿轮）：静默忽略，与 WinUI 里 ShowAsync 抛「已有对话框」不同，
            // 这里不把异常甩给调用方，避免 async void 事件处理器把 UI 线程带崩。
            Console.Error.WriteLine($"DIALOG_IGNORED {dialog.GetType().Name}");
            return;
        }

        var focusManager = TopLevel.GetTopLevel(dialogHost)?.FocusManager;
        IInputElement? previousFocus = focusManager?.GetFocusedElement();
        DialogSession? session = null;

        void OnCloseRequested(object? sender, EventArgs e) => session?.Close(null);

        dialog.CloseRequested += OnCloseRequested;

        // 遮罩淡入：DialogHost 自带的 OverlayBackground 已设为透明，
        // 改由主窗口那一层可控 Opacity 的 Border 负责（关闭时同样淡出）。
        LunarWard.Features.ViewHost.MainWindow.SetDialogScrimVisible(true);
        try
        {
            await DialogHost.Show(dialog, dialogHost, new DialogOpenedEventHandler((_, args) =>
            {
                session = args.Session;
                dialog.NotifyOpened();
                dialog.FocusFirstControl();
            }));
        }
        finally
        {
            LunarWard.Features.ViewHost.MainWindow.SetDialogScrimVisible(false);
            dialog.CloseRequested -= OnCloseRequested;
            dialog.NotifyClosed();
            Gate.Exit();
            RestoreFocus(previousFocus);
        }
    }


    /// <summary>把焦点还给打开对话框之前的控件（关闭按钮 / 齿轮按钮等）。</summary>
    private static void RestoreFocus(IInputElement? previousFocus)
    {
        if (previousFocus is not Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } control)
        {
            return;
        }

        control.Focus();
    }


    /// <summary>
    /// 找浮层宿主：优先用页面自己的 <see cref="DialogHost"/> 祖先（同窗口），否则用主窗口那个。
    /// </summary>
    private static DialogHost? ResolveHost(Control? host)
    {
        if (host is not null)
        {
            if (host is DialogHost self)
            {
                return self;
            }

            if (host.FindAncestorOfType<DialogHost>(includeSelf: true) is { } ancestor)
            {
                return ancestor;
            }
        }

        // 唯一外壳（Features/ViewHost/MainWindow）的浮层；旧壳已删除。
        return LunarWard.Features.ViewHost.MainWindow.CurrentDialogHost;
    }
}
