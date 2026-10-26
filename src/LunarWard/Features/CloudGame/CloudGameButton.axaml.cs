using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.Overlay;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LunarWard.Features.CloudGame;

/// <summary>
/// 云游戏启动按钮，对齐上游 <c>Starward/Features/CloudGame/CloudGameButton.xaml(.xaml.cs)</c>。
/// </summary>
/// <remarks>
/// 平台替换点（见 docs/09 的「十五、抽卡与云游戏」）：
/// <c>[INotifyPropertyChanged]</c>/<c>[RelayCommand]</c> → 手写 <see cref="INotifyPropertyChanged"/> 与 <see cref="IRelayCommand"/>；
/// <c>Launcher.LaunchUriAsync</c> → <see cref="Process.Start(ProcessStartInfo)"/>（走 xdg-open）；
/// WinUI 主题资源 → 本仓库 <c>Styles/Starward.axaml</c> 的等义资源；
/// <c>User32.SetWinEventHook</c> 等 Win32 部分由 <see cref="RunningGameService"/> 承载。
/// </remarks>
public sealed partial class CloudGameButton : UserControl, INotifyPropertyChanged
{

    private readonly ILogger<CloudGameButton> _logger = AppConfig.GetLogger<CloudGameButton>();


    public CloudGameButton()
    {
        this.InitializeComponent();
        StartGameCommand = new AsyncRelayCommand(StartGameAsync);
        InstallGameCommand = new AsyncRelayCommand(InstallGameAsync);
        DataContext = this;
    }


    public ICommand StartGameCommand { get; }

    public ICommand InstallGameCommand { get; }


    public GameId CurrentGameId { get; set; }


    public string? ExePath
    {
        get => field;
        set => SetProperty(ref field, value);
    }


    public string? RunningProcessInfo
    {
        get => field;
        set => SetProperty(ref field, value);
    }



    /// <summary>
    /// 上游为 <c>Flyout.Opened</c> 的 <c>(object sender, object e)</c> 签名；
    /// Avalonia 的 <see cref="Flyout.Opened"/> 是标准 <see cref="EventHandler"/>。
    /// </summary>
    private void Flyout_Opened(object? sender, EventArgs e)
    {
        try
        {
            string key = "";
            RunningProcessInfo = null;
            if (CurrentGameId?.GameBiz == GameBiz.hk4e_cn)
            {
                string? exe = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\GenshinImpactCloudGame", "DisplayIcon", null) as string;
                exe = exe?.Trim('"');
                if (File.Exists(exe))
                {
                    ExePath = exe;
                    string exeName = Path.GetFileName(exe);
                    int session = Process.GetCurrentProcess().SessionId;
                    Process? p = Process.GetProcessesByName(exeName.Replace(".exe", "")).FirstOrDefault(x => x.SessionId == session);
                    if (p is not null)
                    {
                        RunningProcessInfo = $"{Lang.LauncherPage_GameIsRunning}\n{exeName} ({p.Id})";
                    }
                }
            }
            else if (CurrentGameId?.GameBiz == GameBiz.hk4e_global)
            {
                key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Genshin Impact Cloud";
                string? folder = Registry.GetValue(key, "InstallPath", null) as string;
                string? exeName = Registry.GetValue(key, "ExeName", null) as string;
                string? path = Path.Join(folder?.Trim('"'), exeName?.Trim('"'));
                if (File.Exists(path))
                {
                    ExePath = path;
                    int session = Process.GetCurrentProcess().SessionId;
                    Process? p = Process.GetProcessesByName(exeName!.Replace(".exe", "")).FirstOrDefault(x => x.SessionId == session);
                    if (p is not null)
                    {
                        RunningProcessInfo = $"{Lang.LauncherPage_GameIsRunning}\n{exeName} ({p.Id})";
                    }
                }
            }
            else if (CurrentGameId?.GameBiz == GameBiz.nap_cn)
            {
                string? exe = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\ZenlessZoneZeroCloud", "DisplayIcon", null) as string;
                exe = exe?.Trim('"');
                if (File.Exists(exe))
                {
                    ExePath = exe;
                    string exeName = Path.GetFileName(exe);
                    int session = Process.GetCurrentProcess().SessionId;
                    Process? p = Process.GetProcessesByName(exeName.Replace(".exe", "")).FirstOrDefault(x => x.SessionId == session);
                    if (p is not null)
                    {
                        RunningProcessInfo = $"{Lang.LauncherPage_GameIsRunning}\n{exeName} ({p.Id})";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get cloud game state {GameBiz}", CurrentGameId?.GameBiz);
        }
    }





    private async Task StartGameAsync()
    {
        try
        {
            if (File.Exists(ExePath))
            {
                Process? p = Process.Start(new ProcessStartInfo
                {
                    FileName = ExePath!,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(ExePath),
                });
                if (p is not null)
                {
                    RunningGameService.AddRuninngGame(CurrentGameId.GameBiz, p);
                    WeakReferenceMessenger.Default.Send(new GameStartedMessage());
                    await Task.Delay(3000);
                    if (!p.HasExited)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = AppConfig.StarwardExecutePath,
                            Arguments = $"playtime --biz {CurrentGameId.GameBiz} --pid {p.Id}",
                            CreateNoWindow = true,
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start cloud game {GameBiz}", CurrentGameId?.GameBiz);
        }
    }



    private async Task InstallGameAsync()
    {
        try
        {
            string url = "";
            if (CurrentGameId?.GameBiz == GameBiz.hk4e_cn)
            {
                url = "https://ys.mihoyo.com/cloud/#/download";
            }
            else if (CurrentGameId?.GameBiz == GameBiz.hk4e_global)
            {
                url = "https://cloudgenshin.hoyoverse.com/";
            }
            else if (CurrentGameId?.GameBiz == GameBiz.nap_cn)
            {
                url = "https://zzz.mihoyo.com/cloud-feat/";
            }
            if (!string.IsNullOrWhiteSpace(url))
            {
                // 上游 Launcher.LaunchUriAsync(new Uri(url))：本仓库统一走 xdg-open
                await Task.Run(() => Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install cloud game {GameBiz}", CurrentGameId?.GameBiz);
        }
    }




    #region INotifyPropertyChanged

    /// <summary>
    /// <see cref="AvaloniaObject"/> 已实现 <see cref="INotifyPropertyChanged"/>，但只对 AvaloniaProperty 发通知；
    /// 本控件的 CLR 属性走这里。用 <c>new</c> 隐藏基类事件，并把接口分发显式重映射过来。
    /// </summary>
    public new event PropertyChangedEventHandler? PropertyChanged;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropertyChanged += value;
        remove => PropertyChanged -= value;
    }


    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

}
