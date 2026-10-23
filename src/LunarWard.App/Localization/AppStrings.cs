using System.Collections;
using System.ComponentModel;
using System.Globalization;

namespace LunarWard.App.Localization;

/// <summary>
/// 应用自身的 UI 文案（zh-CN / en-US）。
/// </summary>
/// <remarks>
/// <para>
/// 只覆盖 LunarWard 自己的界面文字。<c>LunarWard.Core</c> 的游戏名、区服名等
/// 已经由 Core 的 <c>CoreLang</c> 资源（13 种语言）提供，不要在这里重复。
/// </para>
/// <para>
/// 用法：XAML 里 <c>{Binding [Main.Launch], Source={x:Static loc:AppStrings.Instance}}</c>；
/// 代码里 <c>AppStrings.Instance["Main.Launch"]</c>。
/// 索引器返回的是当前语言的文案；<b>查不到时返回键名本身</b>，这样漏翻译会立刻显形，
/// 而不是显示空白。
/// </para>
/// </remarks>
public sealed class AppStrings : INotifyPropertyChanged
{
    private static readonly Dictionary<string, string> ZhCn = new(StringComparer.Ordinal)
    {
        ["Main.Launch"] = "启动",
        ["Main.Preparing"] = "准备中…",
        ["Main.Starting"] = "启动中…",
        ["Main.WaitingGame"] = "等待游戏…",
        ["Main.WaitingReady"] = "等待就绪…",
        ["Main.StartingHelpers"] = "启动辅助…",
        ["Main.Stop"] = "停止游戏",
        ["Main.Finishing"] = "收尾中…",
        ["Main.Retry"] = "重试",
        ["Main.StatusIdle"] = "就绪",
        ["Main.StatusRunning"] = "游戏运行中",
        ["Main.StatusError"] = "出错了",
        ["Main.Settings"] = "设置",
        ["Main.Shell"] = "环境 shell",
        ["Main.Diagnose"] = "诊断",
        ["Main.Gacha"] = "抽卡",
        ["Main.Environment"] = "环境变量",
        ["Main.Announcements"] = "公告与版本亮点",
        ["Main.NoAnnouncements"] = "暂无公告（离线或接口不可用）",
        ["Main.ExternalPrograms"] = "外部程序（后启动 / 补丁）",
        ["Main.ExternalProgramsHint"] = "后启动与补丁注入是同一个概念：『伴随启动』= 补丁（游戏就绪后拉起），『手动』= 后启动。伴随启动会成为该 wine prefix 的第二个客户端。",
        ["Main.Add"] = "添加",
        ["Main.EnvironmentTable"] = "环境变量（十层合并结果与来源）",
        ["Main.Key"] = "键",
        ["Main.Value"] = "值",
        ["Main.Source"] = "来源层",
        ["Main.NeverLaunched"] = "尚未启动过",
        ["Main.Accumulated"] = "累计",
        ["Settings.Title"] = "设置",
        ["Settings.GameDirectory"] = "游戏目录",
        ["Settings.Scan"] = "扫描",
        ["Settings.Channel"] = "区服渠道",
        ["Settings.Runtime"] = "wine 运行时",
        ["Settings.Rescan"] = "重新扫描",
        ["Settings.WineOptions"] = "wine 与增强",
        ["Settings.Sync"] = "同步原语",
        ["Settings.Borderless"] = "无边框窗口（borderless）",
        ["Settings.WineWayland"] = "winewayland（把 DISPLAY 设为空字符串，而不是删除）",
        ["Settings.TimeoutFix"] = "超时修复补丁（WINE_ENABLE_TIMEOUT_FIX）",
        ["Settings.Fsr"] = "FSR（WINE_FULLSCREEN_FSR）",
        ["Settings.Dx12"] = "DX12（仅绝区零：DXVK_ENABLE_NVAPI + VKD3D）",
        ["Settings.Hud"] = "HUD",
        ["Settings.ReadyGrace"] = "就绪保护期 (ms)",
        ["Settings.Save"] = "保存",
        ["Settings.Language"] = "语言",
        ["Diagnose.Title"] = "诊断",
        ["Shell.Title"] = "环境 shell",
        ["Shell.ExportEnv"] = "导出 env.sh",
    };

    private static readonly Dictionary<string, string> EnUs = new(StringComparer.Ordinal)
    {
        ["Main.Launch"] = "Launch",
        ["Main.Preparing"] = "Preparing…",
        ["Main.Starting"] = "Starting…",
        ["Main.WaitingGame"] = "Waiting for game…",
        ["Main.WaitingReady"] = "Waiting until ready…",
        ["Main.StartingHelpers"] = "Starting helpers…",
        ["Main.Stop"] = "Stop game",
        ["Main.Finishing"] = "Finishing…",
        ["Main.Retry"] = "Retry",
        ["Main.StatusIdle"] = "Ready",
        ["Main.StatusRunning"] = "Game running",
        ["Main.StatusError"] = "Error",
        ["Main.Settings"] = "Settings",
        ["Main.Shell"] = "Environment shell",
        ["Main.Diagnose"] = "Diagnostics",
        ["Main.Gacha"] = "Wish history",
        ["Main.Environment"] = "Environment",
        ["Main.Announcements"] = "Announcements & highlights",
        ["Main.NoAnnouncements"] = "No announcements (offline or API unavailable)",
        ["Main.ExternalPrograms"] = "External programs (post-launch / patches)",
        ["Main.ExternalProgramsHint"] = "Post-launch and patch injection are the same concept: 'auto' = patch (started once the game is ready), 'manual' = post-launch. An auto program becomes the second wine client of that prefix.",
        ["Main.Add"] = "Add",
        ["Main.EnvironmentTable"] = "Environment (ten-layer merge result and sources)",
        ["Main.Key"] = "Key",
        ["Main.Value"] = "Value",
        ["Main.Source"] = "Layer",
        ["Main.NeverLaunched"] = "Never launched",
        ["Main.Accumulated"] = "Total",
        ["Settings.Title"] = "Settings",
        ["Settings.GameDirectory"] = "Game directory",
        ["Settings.Scan"] = "Scan",
        ["Settings.Channel"] = "Channel",
        ["Settings.Runtime"] = "wine runtime",
        ["Settings.Rescan"] = "Rescan",
        ["Settings.WineOptions"] = "wine & enhancements",
        ["Settings.Sync"] = "Sync primitive",
        ["Settings.Borderless"] = "Borderless window",
        ["Settings.WineWayland"] = "winewayland (sets DISPLAY to empty string, not removed)",
        ["Settings.TimeoutFix"] = "Timeout fix patch (WINE_ENABLE_TIMEOUT_FIX)",
        ["Settings.Fsr"] = "FSR (WINE_FULLSCREEN_FSR)",
        ["Settings.Dx12"] = "DX12 (ZZZ only: DXVK_ENABLE_NVAPI + VKD3D)",
        ["Settings.Hud"] = "HUD",
        ["Settings.ReadyGrace"] = "Ready grace (ms)",
        ["Settings.Save"] = "Save",
        ["Settings.Language"] = "Language",
        ["Diagnose.Title"] = "Diagnostics",
        ["Shell.Title"] = "Environment shell",
        ["Shell.ExportEnv"] = "Export env.sh",
    };


    public static AppStrings Instance { get; } = new();


    private string _language = "zh-CN";


    /// <summary>当前语言（<c>zh-CN</c> | <c>en-US</c>）。</summary>
    public string Language
    {
        get => _language;
        set
        {
            var normalized = Normalize(value);
            if (_language == normalized)
            {
                return;
            }

            _language = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            // 索引器绑定：通知 "Item[]" 才会让所有绑定重读
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }


    public IReadOnlyList<string> SupportedLanguages { get; } = ["zh-CN", "en-US"];


    public string this[string key]
    {
        get
        {
            var table = _language == "en-US" ? EnUs : ZhCn;
            return table.TryGetValue(key, out var value) ? value : key;
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;


    /// <summary>把系统语言选项归一化到受支持的语言。</summary>
    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "zh-CN";
        }

        var text = language.Trim().Replace('_', '-');
        if (text.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en-US";
        }

        if (text.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh-CN";
        }

        return "zh-CN";
    }


    /// <summary>所有键（供遗漏检查/测试用）。</summary>
    public static IReadOnlyCollection<string> AllKeys => ZhCn.Keys.ToList();


    /// <summary>英文表是否覆盖了中文表的全部键。</summary>
    public static IReadOnlyList<string> MissingEnglishKeys
        => ZhCn.Keys.Where(k => !EnUs.ContainsKey(k)).ToList();
}
