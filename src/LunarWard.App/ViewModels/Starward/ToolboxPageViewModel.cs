using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.ViewModels;
using LunarWard.App.Views.Starward;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 工具箱页（对应 Starward <c>GameRecordPage</c> 的容器结构：顶部 Tab + 卡片流）。
/// </summary>
/// <remarks>
/// <para>
/// 两个 Tab 直接复用既有 VM：<see cref="DailyNoteViewModel"/>（便笺）与
/// <see cref="AccountViewModel"/>（账号记录）。本类只做容器：Tab 状态、空态判定、
/// 以及把两个 VM 的 <c>PropertyChanged</c> 转成页面级派生属性。
/// </para>
/// <para>Starward 的工具箱用 NavigationView 左侧列表；这里按 UI 合同改成顶部 Pivot。</para>
/// </remarks>
public sealed partial class ToolboxPageViewModel : StarwardPageViewModel
{
    public ToolboxPageViewModel(PageContext context) : base(context)
    {
        DailyNote = new DailyNoteViewModel(context.Services, context.Profile);
        Accounts = new AccountViewModel(context.Services, context.Profile);

        DailyNote.PropertyChanged += OnDailyNoteChanged;
        Accounts.PropertyChanged += OnAccountsChanged;
        Accounts.Accounts.CollectionChanged += OnAccountsCollectionChanged;
        DailyNote.Items.CollectionChanged += OnNoteItemsChanged;

        SelectedTab = 0;
        RefreshDerived();
    }


    // ── Tab ───────────────────────────────────────────────────────────

    /// <summary>0 = 每日便笺，1 = 账号记录（Starward Pivot 的 SelectedIndex）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDailyNoteTab), nameof(IsAccountTab))]
    private int selectedTab;


    public bool IsDailyNoteTab => SelectedTab == 0;

    public bool IsAccountTab => SelectedTab == 1;


    [RelayCommand]
    private void ShowDailyNote() => SelectedTab = 0;


    [RelayCommand]
    private void ShowAccounts() => SelectedTab = 1;


    // ── 子 VM ─────────────────────────────────────────────────────────

    public DailyNoteViewModel DailyNote { get; }

    public AccountViewModel Accounts { get; }


    // ── 每日便笺派生状态 ──────────────────────────────────────────────

    public string DailyNoteHeader => Strings["Toolbox.DailyNote"];

    public string AccountHeader => Strings["Toolbox.Accounts"];

    /// <summary>没有保存 cookie：左卡显示"还没有凭据"，右卡显示空态。</summary>
    public bool HasCookie => !string.IsNullOrWhiteSpace(DailyNote.Cookie);

    /// <summary>右侧便笺卡是否有内容。</summary>
    public bool HasNote => !string.IsNullOrEmpty(DailyNote.PrimaryName) || DailyNote.Items.Count > 0;

    /// <summary>有 cookie 但还没拉过：右侧显示"点刷新"提示（无 cookie 时走另一个空态）。</summary>
    public bool ShowNotePlaceholder => HasCookie && !HasNote;

    public bool IsNotBusy => !DailyNote.IsBusy;

    /// <summary>每日任务完成图标的 Source 文本（✓ / ✗ / 未拉取时为空）。</summary>
    public string DailyTasksGlyph => DailyNote.DailyTasksDone switch
    {
        true => "\uE930",   // Segoe MDL2 CheckMark
        false => "\uE711",  // Cancel
        _ => "",
    };

    public string DailyTasksText => DailyNote.DailyTasksDone switch
    {
        true => Strings["Toolbox.TasksDone"],
        false => Strings["Toolbox.TasksUndone"],
        _ => "",
    };

    /// <summary>完成状态的颜色（成功绿 / 错误红），XAML 里按字符串绑定不到画刷，改用两段可见性。</summary>
    public bool DailyTasksOk => DailyNote.DailyTasksDone == true;

    public bool DailyTasksFailed => DailyNote.DailyTasksDone == false;

    public bool HasDailyTasks => DailyNote.DailyTasksDone is not null;

    public bool HasStatusText => !string.IsNullOrWhiteSpace(DailyNote.StatusText);

    public bool HasDailyNoteError => !string.IsNullOrWhiteSpace(DailyNote.ErrorText);

    public bool HasNoteItems => DailyNote.Items.Count > 0;


    // ── 账号记录派生状态 ──────────────────────────────────────────────

    public bool HasAccounts => Accounts.Accounts.Count > 0;

    public bool IsAccountsEmpty => Accounts.Accounts.Count == 0;

    public bool HasSelectedAccount => Accounts.Selected is not null;

    public bool HasAccountStatus => !string.IsNullOrWhiteSpace(Accounts.StatusText);

    public bool HasAccountError => !string.IsNullOrWhiteSpace(Accounts.ErrorText);

    public string AccountCountText => string.Format(
        CultureInfo.CurrentCulture, Strings["Toolbox.AccountCount"], Accounts.Accounts.Count);


    // ── 页面生命周期 ─────────────────────────────────────────────────

    public override void Dispose()
    {
        DailyNote.PropertyChanged -= OnDailyNoteChanged;
        Accounts.PropertyChanged -= OnAccountsChanged;
        Accounts.Accounts.CollectionChanged -= OnAccountsCollectionChanged;
        DailyNote.Items.CollectionChanged -= OnNoteItemsChanged;
        base.Dispose();
    }


    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(HasCookie));
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(ShowNotePlaceholder));
        OnPropertyChanged(nameof(IsNotBusy));
        OnPropertyChanged(nameof(DailyTasksGlyph));
        OnPropertyChanged(nameof(DailyTasksText));
        OnPropertyChanged(nameof(DailyTasksOk));
        OnPropertyChanged(nameof(DailyTasksFailed));
        OnPropertyChanged(nameof(HasDailyTasks));
        OnPropertyChanged(nameof(HasStatusText));
        OnPropertyChanged(nameof(HasDailyNoteError));
        OnPropertyChanged(nameof(HasNoteItems));
        OnPropertyChanged(nameof(HasAccounts));
        OnPropertyChanged(nameof(IsAccountsEmpty));
        OnPropertyChanged(nameof(HasSelectedAccount));
        OnPropertyChanged(nameof(HasAccountStatus));
        OnPropertyChanged(nameof(HasAccountError));
        OnPropertyChanged(nameof(AccountCountText));
    }


    private void OnDailyNoteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DailyNoteViewModel.Cookie) or nameof(DailyNoteViewModel.PrimaryName)
            or nameof(DailyNoteViewModel.DailyTasksDone) or nameof(DailyNoteViewModel.StatusText)
            or nameof(DailyNoteViewModel.ErrorText) or nameof(DailyNoteViewModel.IsBusy))
        {
            RefreshDerived();
        }
    }


    private void OnAccountsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AccountViewModel.Selected) or nameof(AccountViewModel.StatusText)
            or nameof(AccountViewModel.ErrorText) or nameof(AccountViewModel.CurrentAccountText))
        {
            RefreshDerived();
        }
    }


    private void OnAccountsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshDerived();


    private void OnNoteItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshDerived();
}
