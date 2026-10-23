using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Localization;
using LunarWard.Launch;
using LunarWard.Wine;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 外部程序面板的一行（Starward 风格的展开行）。
/// </summary>
/// <remarks>
/// <para>
/// 数据模型沿用 <see cref="LunarWard.Launch.ExternalProgram"/>（后启动与补丁注入是同一个概念，
/// 只靠 <see cref="Trigger"/> 区分），见 <c>docs/04-FEATURES.md</c> §1。
/// 本类只做"模型 ↔ 绑定的适配 + 用户改动通知"，不重新实现合并算法。
/// </para>
/// <para>
/// 任何改动都会调用 <c>onChanged</c>，由页面 VM 负责落盘（<see cref="LauncherConfigDtoSaver"/>）
/// 与刷新警告。
/// </para>
/// </remarks>
public sealed partial class GameSettingProgramViewModel : ObservableObject
{
    private readonly ExternalProgram _program;

    private readonly Action _onChanged;

    private IReadOnlyDictionary<string, string?> _keywords;


    public GameSettingProgramViewModel(
        ExternalProgram program,
        IReadOnlyDictionary<string, string?> keywords,
        Func<IReadOnlyDictionary<string, string?>, IReadOnlyDictionary<string, string?>> exeKeywordMerger,
        Action onChanged)
    {
        _program = program;
        _keywords = keywords;
        BuildKeywords = exeKeywordMerger;
        _onChanged = onChanged;

        TriggerOptions = GameSettingTriggerOption.All;
        ExitOptions = GameSettingExitOption.All;
        SelectedTrigger = TriggerOptions.FirstOrDefault(x => x.Value == program.Trigger) ?? TriggerOptions[1];
        SelectedExitPolicy = ExitOptions.FirstOrDefault(x => x.Value == program.OnGameExit) ?? ExitOptions[0];

        foreach (var entry in program.Env)
        {
            EnvEntries.Add(new GameSettingEnvEntryViewModel(entry, RemoveEnv, _onChanged));
        }

        RefreshDerived();
    }


    /// <summary>关键字表构造器（由页面 VM 提供，含 <c>%exe%</c> 等）。</summary>
    private Func<IReadOnlyDictionary<string, string?>, IReadOnlyDictionary<string, string?>> BuildKeywords { get; }


    // ── 模型 ──────────────────────────────────────────────────────────

    public ExternalProgram Model => _program;

    public string Id => _program.Id;


    // ── 基本字段 ──────────────────────────────────────────────────────

    public string Name
    {
        get => _program.Name;
        set
        {
            if (_program.Name == value)
            {
                return;
            }

            _program.Name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Header));
            NotifyChanged();
        }
    }


    public bool Enabled
    {
        get => _program.Enabled;
        set
        {
            if (_program.Enabled == value)
            {
                return;
            }

            _program.Enabled = value;
            OnPropertyChanged();
            NotifyChanged();
        }
    }


    public string Path
    {
        get => _program.Path;
        set
        {
            if (_program.Path == value)
            {
                return;
            }

            _program.Path = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Header));
            // 路径变了：%game% / %exe% 之类的解析结果要跟着变
            _keywords = BuildKeywords(_keywords);
            RefreshDerived();
            NotifyChanged();
        }
    }


    public string Arguments
    {
        get => string.Join(' ', _program.Args);
        set
        {
            var parsed = CommandLineSplitter.Split(value);
            if (_program.Args.SequenceEqual(parsed, StringComparer.Ordinal))
            {
                return;
            }

            _program.Args = parsed;
            OnPropertyChanged();
            NotifyChanged();
        }
    }


    public string WorkingDirectory
    {
        get => _program.WorkingDirectory ?? "";
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
            if (_program.WorkingDirectory == normalized)
            {
                return;
            }

            _program.WorkingDirectory = normalized;
            OnPropertyChanged();
            NotifyChanged();
        }
    }


    public bool Wait
    {
        get => _program.Wait;
        set
        {
            if (_program.Wait == value)
            {
                return;
            }

            _program.Wait = value;
            OnPropertyChanged();
            RefreshDerived();
            NotifyChanged();
        }
    }


    // ── 触发 / 退出策略 ───────────────────────────────────────────────

    public IReadOnlyList<GameSettingTriggerOption> TriggerOptions { get; }

    public IReadOnlyList<GameSettingExitOption> ExitOptions { get; }


    [ObservableProperty]
    private GameSettingTriggerOption _selectedTrigger = null!;


    partial void OnSelectedTriggerChanged(GameSettingTriggerOption value)
    {
        if (value is null || _program.Trigger == value.Value)
        {
            return;
        }

        _program.Trigger = value.Value;
        RefreshDerived();
        NotifyChanged();
    }


    [ObservableProperty]
    private GameSettingExitOption _selectedExitPolicy = null!;


    partial void OnSelectedExitPolicyChanged(GameSettingExitOption value)
    {
        if (value is null || _program.OnGameExit == value.Value)
        {
            return;
        }

        _program.OnGameExit = value.Value;
        NotifyChanged();
    }


    /// <summary>该行目前在启动计划里的触发描述（Starward 展开行右侧的一行小字）。</summary>
    public string TriggerSummary => _program.Trigger == Trigger.Auto
        ? AppStrings.Instance["GameSetting.ProgramAutoHint"]
        : AppStrings.Instance["GameSetting.ProgramManualHint"];


    // ── 警告 / 建议 ───────────────────────────────────────────────────

    private string _warnings = "";

    private string _suggestion = "";

    private bool _hasWarnings;

    private bool _hasSuggestion;


    /// <summary>安全提示（不阻止启动），见 <c>docs/04-FEATURES.md</c> §1.6。</summary>
    public string Warnings
    {
        get => _warnings;
        private set
        {
            if (SetProperty(ref _warnings, value))
            {
                OnPropertyChanged(nameof(HasWarnings));
            }
        }
    }


    /// <summary>逐条警告（区分颜色：文件不存在这类是 Error，其余是 Caution）。</summary>
    public ObservableCollection<GameSettingWarningRow> WarningRows { get; } = [];


    /// <summary>折叠行上的警告角标（不给文字，Starward 风格是行内小图标 + 计数）。</summary>
    public string WarningBadge => WarningRows.Count switch
    {
        0 => "",
        1 => "!",
        _ => $"!{WarningRows.Count}",
    };



    public bool HasWarnings
    {
        get => _hasWarnings;
        private set => SetProperty(ref _hasWarnings, value);
    }


    /// <summary>启发式建议（仅 UI，不落盘），见 §1.7。</summary>
    public string Suggestion
    {
        get => _suggestion;
        private set
        {
            if (SetProperty(ref _suggestion, value))
            {
                OnPropertyChanged(nameof(HasSuggestion));
            }
        }
    }


    public bool HasSuggestion
    {
        get => _hasSuggestion;
        private set => SetProperty(ref _hasSuggestion, value);
    }


    /// <summary>有任一 Error 级警告（文件不存在等）。</summary>
    public bool HasErrorWarning { get; private set; }


    private void RefreshDerived()
    {
        var warnings = _program.GetWarnings(_keywords).ToList();
        Warnings = string.Join("\n", warnings.Select(x => x.Message));
        HasWarnings = warnings.Count > 0;
        HasErrorWarning = warnings.Any(x => x.Severity == WarningSeverity.Error);

        WarningRows.Clear();
        foreach (var warning in warnings)
        {
            WarningRows.Add(new GameSettingWarningRow(warning.Message, warning.Severity == WarningSeverity.Error));
        }

        OnPropertyChanged(nameof(WarningBadge));

        Suggestion = _program.GetSuggestion() ?? "";
        HasSuggestion = Suggestion.Length > 0;

        OnPropertyChanged(nameof(HasErrorWarning));
        OnPropertyChanged(nameof(TriggerSummary));
        OnPropertyChanged(nameof(ResolvedPath));
    }


    /// <summary>关键字替换后的实际路径（Starward 展开行的灰色路径提示）。</summary>
    public string ResolvedPath
    {
        get
        {
            var replaced = KeywordReplacer.Replace(_program.Path, _keywords);
            return string.IsNullOrEmpty(replaced) ? "" : replaced;
        }
    }


    /// <summary>展开行的标题：名称 + 未启用标记。</summary>
    public string Header => string.IsNullOrWhiteSpace(_program.Name)
        ? AppStrings.Instance["GameSetting.ProgramUnnamed"]
        : _program.Name;


    // ── 三态 env ──────────────────────────────────────────────────────

    public ObservableCollection<GameSettingEnvEntryViewModel> EnvEntries { get; } = [];


    [RelayCommand]
    private void AddEnv()
    {
        var entry = new EnvEntry { Key = "NEW_VAR", Kind = EnvPatchKind.Override, Value = "" };
        _program.Env.Add(entry);
        EnvEntries.Add(new GameSettingEnvEntryViewModel(entry, RemoveEnv, _onChanged));
        NotifyChanged();
    }


    private void RemoveEnv(GameSettingEnvEntryViewModel item)
    {
        _program.Env.Remove(item.Model);
        EnvEntries.Remove(item);
        NotifyChanged();
    }


    [RelayCommand]
    private void RemoveEnvEntry(GameSettingEnvEntryViewModel? item)
    {
        if (item is not null)
        {
            RemoveEnv(item);
        }
    }


    // ── 立即启动 ──────────────────────────────────────────────────────

    /// <summary>仅游戏在 <c>Running</c> 相位时可用（Starward 在其它相位禁用 + 提示）。</summary>
    [ObservableProperty]
    private bool _canStartNow;


    /// <summary>点了「立即启动」：页面 VM 负责真正拉起并回填结果。</summary>
    public event EventHandler<string>? StartRequested;


    [RelayCommand]
    private void StartNow() => StartRequested?.Invoke(this, Id);


    // ── 视图状态（展开 / 折叠）────────────────────────────────────────

    [ObservableProperty]
    private bool _isExpanded;


    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;


    [ObservableProperty]
    private bool _isEditing;


    private void NotifyChanged()
    {
        // 名称/路径/触发/参数都影响警告与建议
        RefreshDerived();
        _onChanged();
    }
}


/// <summary>
/// 三态环境变量的一行：<c>继承 / 覆盖 / 删除</c> + 「设为空值」副选项。
/// </summary>
/// <remarks>
/// "输入框清空 → 默认 <c>Remove</c>"、"勾上『设为空值』→ <c>Override("")</c>" 的规则
/// 见 <c>docs/04-FEATURES.md</c> §1.4 与 <c>docs/00-DECISIONS.md</c> D7。
/// 这里直接复用旧 <c>MainWindowViewModel</c> 的行为，只补了"键改名"的通知。
/// </remarks>
public sealed partial class GameSettingEnvEntryViewModel : ObservableObject
{
    private readonly Action<GameSettingEnvEntryViewModel> _onRemove;

    private readonly Action _onChanged;

    private bool _syncing;


    public GameSettingEnvEntryViewModel(
        EnvEntry model,
        Action<GameSettingEnvEntryViewModel> onRemove,
        Action onChanged)
    {
        Model = model;
        _onRemove = onRemove;
        _onChanged = onChanged;

        Kinds = GameSettingEnvKindOption.All;
        SelectedKind = Kinds.FirstOrDefault(x => x.Value == model.Kind) ?? Kinds[0];
        _value = model.Value ?? "";
        _setEmptyValue = model.Kind == EnvPatchKind.Override && model.Value == "";
    }


    public EnvEntry Model { get; }

    public IReadOnlyList<GameSettingEnvKindOption> Kinds { get; }


    public string Key
    {
        get => Model.Key;
        set
        {
            if (Model.Key == value)
            {
                return;
            }

            Model.Key = value;
            OnPropertyChanged();
            _onChanged();
        }
    }


    [ObservableProperty]
    private GameSettingEnvKindOption _selectedKind = null!;


    partial void OnSelectedKindChanged(GameSettingEnvKindOption value)
    {
        if (_syncing || value is null || Model.Kind == value.Value)
        {
            return;
        }

        Model.Kind = value.Value;
        if (value.Value == EnvPatchKind.Remove)
        {
            _syncing = true;
            Value = "";
            SetEmptyValue = false;
            _syncing = false;
        }
        else if (value.Value == EnvPatchKind.Override && Model.Value is null)
        {
            _syncing = true;
            Value = "";
            _syncing = false;
        }

        OnPropertyChanged(nameof(IsOverride));
        OnPropertyChanged(nameof(IsEditable));
        _onChanged();
    }


    public bool IsOverride => Model.Kind == EnvPatchKind.Override;

    public bool IsEditable => Model.Kind == EnvPatchKind.Override;


    [ObservableProperty]
    private string _value = "";


    partial void OnValueChanged(string value)
    {
        if (_syncing)
        {
            return;
        }

        if (string.IsNullOrEmpty(value) && !SetEmptyValue)
        {
            Model.Kind = EnvPatchKind.Remove;
            Model.Value = null;
        }
        else
        {
            Model.Kind = EnvPatchKind.Override;
            Model.Value = value;
        }

        SyncKindFromModel();
        _onChanged();
    }


    /// <summary>"设为空值"：勾上则 <c>Override("")</c> 而不是 <c>Remove</c>。</summary>
    [ObservableProperty]
    private bool _setEmptyValue;


    partial void OnSetEmptyValueChanged(bool value)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        if (value)
        {
            Model.Kind = EnvPatchKind.Override;
            Model.Value = "";
            Value = "";
        }
        else if (Model.Value == "")
        {
            Model.Kind = EnvPatchKind.Remove;
            Model.Value = null;
        }

        _syncing = false;
        SyncKindFromModel();
        _onChanged();
    }


    private void SyncKindFromModel()
    {
        var match = Kinds.FirstOrDefault(x => x.Value == Model.Kind);
        if (match is not null && !ReferenceEquals(match, SelectedKind))
        {
            _syncing = true;
            SelectedKind = match;
            _syncing = false;
        }

        OnPropertyChanged(nameof(IsOverride));
        OnPropertyChanged(nameof(IsEditable));
    }


    [RelayCommand]
    private void Remove() => _onRemove(this);
}


/// <summary>一条外部程序警告（Error 用错误色，其余用 Caution 色）。</summary>
public sealed record GameSettingWarningRow(string Message, bool IsError);
