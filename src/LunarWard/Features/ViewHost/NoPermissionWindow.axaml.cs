using Avalonia.Interactivity;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LunarWard.Features.ViewHost;

/// <summary>
/// 目录无写权限时的提示窗口（重启提权 / 关闭）。<b>移植自</b>
/// <c>Starward/src/Starward/Features/ViewHost/NoPermissionWindow.xaml(.cs)</c>。
/// </summary>
/// <remarks>
/// <para>
/// <b>平台替换</b>：
/// </para>
/// <list type="bullet">
/// <item>Win32 <c>runas</c>（UAC 提权）→ <c>pkexec</c>（PolicyKit，Linux 上等价的提权方式），
/// 参数逐字透传当前命令行；<c>pkexec</c> 不存在时降级为普通重启并提示无法自动提权。</item>
/// <item><c>Launcher.LaunchUriAsync(folder)</c> → <c>xdg-open</c>（Linux 桌面打开目录的标准方式）。</item>
/// <item>WinUI <c>OverlappedPresenter</c> 的不可缩放/不可最大化 → Avalonia
/// <c>CanResize/CanMaximize/CanMinimize=false</c>；窗口尺寸由构造方 <c>CenterInScreen(460, 280)</c> 给出。</item>
/// </list>
/// </remarks>
public sealed partial class NoPermissionWindow : WindowEx
{


    private readonly TaskCompletionSource _taskCompletionSource;


    public string Folder { get; set; }


    public NoPermissionWindow(string folder)
    {
        InitializeComponent();
        InitializeWindow();
        _taskCompletionSource = new();
        Folder = folder;
    }


    private void InitializeWindow()
    {
        Closed += NoPermissionWindow_Closed;
        CenterInScreen(460, 280);
    }


    private void NoPermissionWindow_Closed(object? sender, EventArgs e)
    {
        _taskCompletionSource.TrySetResult();
    }


    public async Task WaitAsync()
    {
        Activate();
        await _taskCompletionSource.Task;
    }


    /// <summary>
    /// 以管理员身份重启：Linux 走 <c>pkexec</c>（等价于上游 <c>Verb = "runas"</c>）。
    /// </summary>
    private void RestartAsAdminButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return;
            }
            string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            ProcessStartInfo info;
            if (File.Exists("/usr/bin/pkexec"))
            {
                info = new ProcessStartInfo("/usr/bin/pkexec") { UseShellExecute = false };
                info.ArgumentList.Add(exe);
            }
            else
            {
                Log.Warning("pkexec 不存在，无法自动提权，改为普通重启");
                info = new ProcessStartInfo(exe) { UseShellExecute = false };
            }
            foreach (string arg in args)
            {
                info.ArgumentList.Add(arg);
            }
            Process.Start(info);
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "以管理员身份重启失败");
            InAppToast.Show(this, ex.Message);
        }
    }


    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }


    private async void FolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(Folder))
        {
            return;
        }
        try
        {
            // 平台替换：Windows 的 Launcher.LaunchUriAsync → xdg-open
            Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { Folder }, UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开目录失败 {Folder}", Folder);
        }
        await Task.CompletedTask;
    }


}
