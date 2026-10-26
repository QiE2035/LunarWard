using LunarWard.Launch;
using LunarWard.Platform;

namespace LunarWard.ViewModels;

/// <summary>启动报告查看器（<c>launch-report.json</c> 的只读视图）。</summary>
public sealed class DiagnosticViewModel
{
    public DiagnosticViewModel(LaunchReport report, IXdgPaths paths)
    {
        Report = report;
        Json = report.ToJson();
        ReportPath = paths.LaunchReportFile;
        GameLogPath = paths.GameLogFile;
        TroubleshootingSteps =
        [
            "1. pgrep -af 'wineserver|winedevice'    —— 有输出说明 prefix 是「热」的",
            "2. 检查 Prepare 相位是否有 PrefixNotClean",
            "3. 检查 AwaitImage 是否命中（有没有找到游戏 exe）",
            "4. 检查 AwaitReady 的 signal 是什么：Grace 且很快 → 保护期可能更长，调大 grace",
            "5. 看 game.log 是否有 wine 报错",
            "6. 看游戏目录的 driverError.log 是否有 initDriver Failed",
        ];
    }


    public LaunchReport Report { get; }

    public string Json { get; }

    public string ReportPath { get; }

    public string GameLogPath { get; }

    public IReadOnlyList<string> TroubleshootingSteps { get; }

    public string Summary =>
        $"{(Report.Success ? "成功" : "失败")} · 退出码 {Report.ExitCode}"
        + (Report.ReadySignal is null ? "" : $" · 信号 {Report.ReadySignal}")
        + (Report.GamePid is null ? "" : $" · pid {Report.GamePid}");
}
