using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Launch;
using LunarWard.Launch.Database;
using LunarWard.Launch.Uigf4;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// UIGF v4.2 窗口（复刻 Starward <c>Features/Gacha/UIGF/UIGF4GachaWindow.xaml</c>）的数据侧。
/// </summary>
/// <remarks>
/// <para>
/// 左侧两个导航项（导出记录 / 导入记录），右侧内容区；导出页列出现有存档并勾选，
/// 导入页选文件后逐条列出、可改时区偏移。
/// </para>
/// <para>
/// 文件选择、写文件、错误提示都留在视图层（<c>StorageProvider</c> 只能从 <c>TopLevel</c> 拿），
/// 这里只负责建 JSON、解析文件、写存档。
/// </para>
/// </remarks>
public sealed partial class Uigf4GachaWindowViewModel : ObservableObject
{
    private readonly Uigf4Service _service;

    private readonly Localization.AppStrings _strings = Localization.AppStrings.Instance;

    private bool _exportLoaded;


    public Uigf4GachaWindowViewModel(Uigf4Service service)
    {
        _service = service;
        ExportItems = [];
        ImportItems = [];
        LoadExportItems();
    }


    /// <summary>XAML 文案索引器。</summary>
    public Localization.AppStrings Strings => _strings;

    /// <summary>导出页的存档行（页面首次显示时加载）。</summary>
    public ObservableCollection<Uigf4ExportItemViewModel> ExportItems { get; }

    /// <summary>导入页选完文件后的存档行。</summary>
    public ObservableCollection<Uigf4ImportItemViewModel> ImportItems { get; }


    // ── 导航 ──────────────────────────────────────────────────────────

    /// <summary>当前页：0 = 导出，1 = 导入（Starward 是两个 NavigationView 项）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExportPage))]
    [NotifyPropertyChangedFor(nameof(IsImportPage))]
    private int _selectedPage;

    public bool IsExportPage => SelectedPage == 0;

    public bool IsImportPage => SelectedPage == 1;

    partial void OnSelectedPageChanged(int value)
    {
        if (value == 0 && !_exportLoaded)
        {
            LoadExportItems();
        }
    }


    // ── 状态 ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private string? _errorText;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>次要色结果行（导出/导入完成后的提示）。</summary>
    [ObservableProperty]
    private string? _statusText;

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    partial void OnStatusTextChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    [ObservableProperty]
    private bool _isBusy;


    // ── 导出页 ────────────────────────────────────────────────────────

    public bool HasExports => ExportItems.Count > 0;

    public bool HasNoExports => ExportItems.Count == 0;


    /// <summary>重新从本地存档读列表（构造时执行一次，切回导出页时不会重复加载）。</summary>
    public void LoadExportItems()
    {
        ExportItems.Clear();
        foreach (var summary in _service.GetArchives())
        {
            var row = new Uigf4ExportItemViewModel(summary, GameDisplayName(summary.Game));
            row.CheckedChanged += _ => OnPropertyChanged(nameof(AllExportsChecked));
            ExportItems.Add(row);
        }
        _exportLoaded = true;
        OnPropertyChanged(nameof(HasExports));
        OnPropertyChanged(nameof(HasNoExports));
        OnPropertyChanged(nameof(AllExportsChecked));
    }


    /// <summary>「全选」勾选态：全部勾上为 true，部分勾选为 null。</summary>
    public bool? AllExportsChecked
    {
        get
        {
            if (ExportItems.Count == 0)
            {
                return false;
            }
            int checkedCount = ExportItems.Count(x => x.IsChecked);
            return checkedCount == 0 ? false : checkedCount == ExportItems.Count ? true : null;
        }
        set
        {
            if (value is null)
            {
                return;
            }
            foreach (var item in ExportItems)
            {
                item.IsChecked = value.Value;
            }
            OnPropertyChanged();
        }
    }


    public string SuggestedExportFileName => Uigf4Service.FileName(DateTime.Now);


    /// <summary>按当前勾选生成 UIGF v4.2 JSON；一个都没勾返回 null。</summary>
    public string? BuildExportJson()
    {
        var selected = ExportItems.Where(x => x.IsChecked).Select(x => x.Summary).ToList();
        return selected.Count == 0 ? null : _service.BuildJson(selected);
    }


    // ── 导入页 ────────────────────────────────────────────────────────

    public bool HasImportItems => ImportItems.Count > 0;

    /// <summary>「全选」勾选态（导入页）。</summary>
    public bool? AllImportsChecked
    {
        get
        {
            if (ImportItems.Count == 0)
            {
                return false;
            }
            int checkedCount = ImportItems.Count(x => x.IsChecked);
            return checkedCount == 0 ? false : checkedCount == ImportItems.Count ? true : null;
        }
        set
        {
            if (value is null)
            {
                return;
            }
            foreach (var item in ImportItems)
            {
                item.IsChecked = value.Value;
            }
            OnPropertyChanged();
        }
    }


    /// <summary>清空并重新按文件内容填导入列表（无法解析时抛 <see cref="Uigf4ImportException"/>）。</summary>
    public void LoadImportFile(string path) => FillImport(_service.ParseFile(path), Path.GetFileName(path));


    /// <summary>自检用：直接从一段 JSON 填列表（不经过文件）。</summary>
    public void LoadImportJson(string json, string displayName)
        => FillImport(_service.ParseJson(json, displayName), displayName);


    private void FillImport(IReadOnlyList<Uigf4ImportArchive> archives, string displayName)
    {
        ErrorText = null;
        StatusText = null;
        ImportItems.Clear();
        foreach (var archive in archives)
        {
            var row = new Uigf4ImportItemViewModel(archive, GameDisplayName(archive.Game));
            row.CheckedChanged += _ => OnPropertyChanged(nameof(AllImportsChecked));
            ImportItems.Add(row);
        }
        SourceFileName = displayName;
        OnPropertyChanged(nameof(HasImportItems));
        OnPropertyChanged(nameof(AllImportsChecked));
    }


    /// <summary>选中的文件（显示在列表上方）。</summary>
    [ObservableProperty]
    private string? _sourceFileName;


    /// <summary>
    /// 把勾选的存档写入本地存档；返回有记录缺少物品名的提示（上游同款）。
    /// </summary>
    public bool ImportSelected()
    {
        var selected = ImportItems.Where(x => x.IsChecked).Select(x => x.Archive).ToList();
        if (selected.Count == 0)
        {
            return true;
        }

        bool success = true;
        foreach (var archive in selected)
        {
            var result = _service.Import(archive);
            success &= result.Success;
        }

        // 导入会改变存档，导出列表下次进入时重建
        _exportLoaded = false;
        return success;
    }


    // ── 命令（文件对话框由窗口 code-behind 承接） ──────────────────────

    /// <summary>要写文件：(JSON 内容, 建议文件名)。由窗口弹「保存文件」并落盘。</summary>
    public event Action<string, string>? ExportRequested;

    /// <summary>由窗口弹「打开文件」；选好后回调 <see cref="LoadImportFile"/>。</summary>
    public event Action? SelectFileRequested;

    [RelayCommand]
    private void Export()
    {
        string? json = BuildExportJson();
        if (json is null)
        {
            StatusText = Strings["Gacha.Uigf.NothingSelected"];
            return;
        }
        ExportRequested?.Invoke(json, SuggestedExportFileName);
    }


    [RelayCommand]
    private void SelectFile() => SelectFileRequested?.Invoke();


    [RelayCommand]
    private void Import()
    {
        ErrorText = null;
        StatusText = null;
        try
        {
            bool allFieldsPresent = ImportSelected();
            StatusText = allFieldsPresent
                ? Strings["Gacha.Uigf.Imported"]
                : Strings["Gacha.Uigf.ImportMissingNames"];
        }
        catch (Uigf4ImportException ex)
        {
            ErrorText = DescribeImportError(ex);
        }
    }


    /// <summary>导出落盘成功后由窗口调用，显示结果路径。</summary>
    public void NotifyExported(string path) => StatusText = string.Format(Strings["Gacha.Uigf.Exported"], path);


    private string DescribeImportError(Uigf4ImportException ex) => ex.Kind switch
    {
        Uigf4ImportErrorKind.InvalidFile => string.Format(Strings["Gacha.Uigf.Error.InvalidFile"], ex.Path),
        Uigf4ImportErrorKind.MissingField => string.Format(Strings["Gacha.Uigf.Error.MissingField"], ex.Field ?? ""),
        Uigf4ImportErrorKind.UidMismatch => string.Format(Strings["Gacha.Uigf.Error.UidMismatch"], ex.ExpectedUid, ex.FoundUid),
        Uigf4ImportErrorKind.UnsupportedSection => Strings["Gacha.Uigf.Unsupported"],
        _ => ex.Message,
    };


    internal static string GameDisplayName(string kind) => kind switch
    {
        GachaLogStore.Genshin => "原神",
        GachaLogStore.StarRail => "星穹铁道",
        GachaLogStore.Zzz => "绝区零",
        Uigf4Service.BeyondGame => "千星奇域",
        _ => kind,
    };
}


/// <summary>导出页的一行：一个「游戏 + UID + 语言」存档。</summary>
public sealed partial class Uigf4ExportItemViewModel : ObservableObject
{
    public Uigf4ExportItemViewModel(GachaArchiveSummary summary, string gameName)
    {
        Summary = summary;
        GameName = gameName;
        IsChecked = true;
    }


    public Localization.AppStrings Strings => Localization.AppStrings.Instance;

    public GachaArchiveSummary Summary { get; }

    public string GameName { get; }

    public long Uid => Summary.Uid;

    /// <summary>行标题：<c>原神 100000001</c>（与上游 <c>GameName + Uid</c> 一致）。</summary>
    public string Title => $"{GameName} {Uid}";

    public string CountText => string.Format(
        Localization.AppStrings.Instance["Gacha.Uigf.RecordCount"],
        Summary.Count);

    /// <summary>语言徽标（zh-cn / en-us ...），未记录时显示"—"。</summary>
    public string LangText => string.IsNullOrWhiteSpace(Summary.Lang) ? "—" : Summary.Lang;

    public string LastItemTimeText => Summary.LastItemTime == default
        ? ""
        : Summary.LastItemTime.ToString("yyyy-MM-dd HH:mm:ss");

    public string LastItemName => Summary.LastItemName;

    [ObservableProperty]
    private bool _isChecked;

    partial void OnIsCheckedChanged(bool value) => CheckedChanged?.Invoke(this);

    /// <summary>窗口 VM 用来更新「全选」三态。</summary>
    public event Action<Uigf4ExportItemViewModel>? CheckedChanged;
}


/// <summary>导入页的一行：文件里的一个存档，可改时区偏移。</summary>
public sealed partial class Uigf4ImportItemViewModel : ObservableObject
{
    public Uigf4ImportItemViewModel(Uigf4ImportArchive archive, string gameName)
    {
        Archive = archive;
        GameName = gameName;
        IsChecked = false;
    }


    public Localization.AppStrings Strings => Localization.AppStrings.Instance;

    public Uigf4ImportArchive Archive { get; }

    public string GameName { get; }

    public long Uid => Archive.Uid;

    public string Title => $"{GameName} {Uid}";

    public string CountText => string.Format(
        Localization.AppStrings.Instance["Gacha.Uigf.RecordCount"],
        Archive.Count);

    public string LastItemTimeText => Archive.LastItemTime == default
        ? ""
        : Archive.LastItemTime.ToString("yyyy-MM-dd HH:mm:ss");

    public string LastItemName => Archive.LastItemName;

    /// <summary>千星奇域等暂不支持写入的区段：勾选框禁用并显示说明。</summary>
    public bool IsUnsupported => Archive.Unsupported;

    public bool CanEdit => !Archive.Unsupported;


    [ObservableProperty]
    private bool _isChecked;

    partial void OnIsCheckedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditTimezone));
        CheckedChanged?.Invoke(this);
    }

    public bool CanEditTimezone => IsChecked && !Archive.Unsupported;


    /// <summary>时区偏移（小时，-12~14）。写到 <see cref="Uigf4ImportArchive.Timezone"/>。</summary>
    public int Timezone
    {
        get => Archive.Timezone;
        set
        {
            if (Archive.Timezone == value)
            {
                return;
            }
            Archive.Timezone = value;
            OnPropertyChanged();
        }
    }


    public event Action<Uigf4ImportItemViewModel>? CheckedChanged;
}
