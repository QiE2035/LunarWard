using Avalonia.Controls;
using Avalonia.Threading;
using Iciclecreek.Terminal;
using LunarWard.App.ViewModels;

namespace LunarWard.App.Views;

/// <summary>
/// 环境 shell 窗口。终端控件用 <c>Iciclecreek.Avalonia.Terminal</c>
/// （<see cref="TerminalView"/>），PTY 走 Porta.Pty 的原生 <c>openpty</c>。
/// </summary>
public partial class ShellWindow : Window
{
    private TerminalView? _terminal;


    public ShellWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }


    private ShellViewModel? ViewModel => DataContext as ShellViewModel;


    private void OnOpened(object? sender, EventArgs e)
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        _terminal = new TerminalView
        {
            StartingDirectory = vm.StartingDirectory,
            EnvironmentVariables = vm.EnvironmentVariables,
            FontFamily = new Avalonia.Media.FontFamily("monospace"),
            FontSize = 13,
        };

        vm.Terminal = _terminal;
        TerminalHost.Content = _terminal;
        TerminalHint.IsVisible = false;

        // 启动 bash（若无则退到 sh）
        var shell = Environment.GetEnvironmentVariable("SHELL");
        if (string.IsNullOrEmpty(shell) || !File.Exists(shell))
        {
            shell = File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
        }

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await _terminal.LaunchProcess(vm.StartingDirectory, shell, ["-l"]);
            }
            catch (Exception ex)
            {
                // 退路：只显示日志 + 导出 env.sh（docs/04-FEATURES.md §6.4）
                TerminalHint.IsVisible = true;
                TerminalHint.Text = $"PTY 启动失败：{ex.Message}\n可以点右上角『导出 env.sh』到外部终端使用。";
            }
        });
    }


    protected override void OnClosed(EventArgs e)
    {
        try
        {
            _terminal?.Kill();
            _terminal?.Dispose();
        }
        catch (Exception)
        {
            // ignore
        }

        _terminal = null;
        base.OnClosed(e);
    }
}
