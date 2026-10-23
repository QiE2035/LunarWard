using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Iciclecreek.Terminal;
using LunarWard.Launch;
using LunarWard.Platform;

namespace LunarWard.App.ViewModels;

/// <summary>
/// 环境 shell：<b>复用</b> <see cref="LaunchPlan.Env"/>，绝不另写一套环境构建逻辑。
/// </summary>
/// <remarks>见 <c>docs/04-FEATURES.md</c> §6.2 / §6.3。</remarks>
public sealed partial class EnvironmentShellViewModel : ObservableObject
{
    private readonly LaunchPlan _plan;

    private readonly IXdgPaths _paths;


    public EnvironmentShellViewModel(LaunchPlan plan, IXdgPaths paths)
    {
        _plan = plan;
        _paths = paths;
        CommandPresets =
        [
            "winecfg",
            "wine regedit",
            "wineserver -k",
            "wine --version",
            $"wine {plan.GameImage}",
            "ls \"$WINEPREFIX/drive_c\"",
        ];
        Reload();
    }


    /// <summary>本 shell 是否已经有活动终端（由 View 设置）。</summary>
    public TerminalView? Terminal { get; set; }


    public string Title => $"{_plan.ProfileId} · 环境 shell";

    public string GameDirectory => _plan.GameDirectory;

    public string Prefix => _plan.Prefix;

    public IReadOnlyList<string> CommandPresets { get; }

    public ObservableCollection<EnvironmentRowViewModel> EnvironmentRows { get; } = [];


    [ObservableProperty]
    private string? errorText;


    [ObservableProperty]
    private string exportScript = "";


    /// <summary>按当前计划刷新 env 表格与导出的 env.sh。</summary>
    public void Reload()
    {
        EnvironmentRows.Clear();
        foreach (var (key, value) in _plan.Env.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            EnvironmentRows.Add(new EnvironmentRowViewModel(key, value, _plan.EnvSources.GetValueOrDefault(key, "?")));
        }

        ExportScript = BuildExportScript();
    }


    /// <summary>生成可直接粘贴到外部终端的 <c>env.sh</c>。</summary>
    public string BuildExportScript()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("#!/bin/sh");
        builder.AppendLine($"# LunarWard 环境 shell —— profile {_plan.ProfileId}");
        builder.AppendLine($"cd {Quote(_plan.GameDirectory)}");
        foreach (var (key, value) in _plan.Env.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"export {key}={Quote(value)}");
        }

        return builder.ToString();
    }


    private static string Quote(string value)
        => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";


    /// <summary>把 <c>env.sh</c> 写到数据目录，返回路径。</summary>
    public string WriteExportScript()
    {
        var path = Path.Join(_paths.CacheHome, "env.sh");
        AtomicFile.WriteAllText(path, ExportScript);
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception)
        {
            // ignore
        }

        return path;
    }


    [RelayCommand]
    private async Task RunPresetAsync(string command)
    {
        if (Terminal is null)
        {
            ErrorText = "终端尚未就绪";
            return;
        }

        await Terminal.SendInputAsync(command + "\n");
    }


    [RelayCommand]
    private void ExportEnv()
    {
        try
        {
            var path = WriteExportScript();
            ErrorText = $"已导出：{path}";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    /// <summary>终端里要设置的环境变量（交给 <c>TerminalView.EnvironmentVariables</c>）。</summary>
    public IDictionary<string, string> EnvironmentVariables
        => new Dictionary<string, string>(_plan.Env, StringComparer.Ordinal);


    /// <summary>工作目录 = 游戏目录。</summary>
    public string StartingDirectory => _plan.GameDirectory;
}
