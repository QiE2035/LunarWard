using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using LunarWard.App.Services;
using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Launch.Database;
using LunarWard.Launch.Gacha;

namespace LunarWard.App.Views.Starward.Pages;

/// <summary>
/// 千星奇域颂愿（Starward <c>Features/Gacha/GenshinBeyondGachaPage.xaml.cs</c>）。
///
/// <para>页面本身只是两张统计卡的容器，交互都落在页面代码里：</para>
/// <list type="bullet">
///   <item>页签切换（源码 Pivot + Segmented）→ 同步两张卡的 <c>SelectedTabIndex</c>；</item>
///   <item>「输入 URL」Flyout → 复用账号记录页的 <see cref="SelfQueryInputUrlWindow"/> 对话框；</item>
///   <item>「复制 URL」→ 先把 URL 写进剪贴板再提示。</item>
/// </list>
/// </summary>
public partial class GenshinBeyondGachaPage : StarwardPage
{

    private readonly GenshinBeyondGachaPageViewModel _viewModel;


    public GenshinBeyondGachaPage(PageContext context) : base(context)
    {
        InitializeComponent();
        if (CaptureMode.BeyondSeed)
        {
            SeedSampleData();
        }
        _viewModel = new GenshinBeyondGachaPageViewModel(context);
        if (CaptureMode.BeyondSecondTab)
        {
            SelectTab(1);
        }
        if (CaptureMode.BeyondPane)
        {
            _viewModel.IsPaneOpen = true;
        }
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel.Dispose();
    }


    // ── 页签（源码 Pivot + Segmented：4★ / 3★、5★ / 4★）──────────────

    private void OnSelectFirstTab(object? sender, RoutedEventArgs e)
    {
        SelectTab(0);
    }


    private void OnSelectSecondTab(object? sender, RoutedEventArgs e)
    {
        SelectTab(1);
    }


    /// <summary>两张卡用同一套页签，切换时一起改（源码也是两个 Pivot 各自跟随 Segmented 的选中项）。</summary>
    private void SelectTab(int index)
    {
        if (_viewModel.Card1000 is { } card1000)
        {
            card1000.SelectedTabIndex = index;
        }
        if (_viewModel.Card2000 is { } card2000)
        {
            card2000.SelectedTabIndex = index;
        }
    }


    // ── 设置 Flyout：输入 URL / 复制 URL ───────────────────────────────

    private async void OnShowInputUrlFlyout(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }
            var dialog = new SelfQueryInputUrlWindow(
                _viewModel.Strings["Gacha.InputUrl"],
                _viewModel.Strings["SelfQuery.StepsToGetURL"],
                _viewModel.Strings["SelfQuery.InputURLSteps"],
                _viewModel.Strings["SelfQuery.InputURLNote"],
                _viewModel.Strings["SelfQuery.Confirm"],
                _viewModel.Strings["Common.Cancel"]);
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed && !string.IsNullOrWhiteSpace(dialog.Url))
            {
                await _viewModel.ApplyInputUrlAsync(dialog.Url);
            }
        }
        catch (Exception ex)
        {
            _viewModel.Log.Error("Beyond gacha input url", ex);
        }
    }


    private async void OnCopyUrlClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var url = await _viewModel.GetCopyableUrlAsync();
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(url);
            }
        }
        catch (Exception ex)
        {
            _viewModel.Log.Error("Beyond gacha copy url", ex);
        }
    }


    // ── 自检：写入样例记录，供截图渲染两张统计卡 ───────────────────────

    /// <summary>
    /// 截图自检用（<c>--beyond-seed</c>）。写的是 <c>LunarWardDatabase.db</c> 的
    /// <c>GenshinBeyondGachaItem</c> 表，与页面共用同一个库，因此必须先于 VM 构造执行。
    /// </summary>
    internal static void SeedSampleData()
    {
        try
        {
            string file = CaptureMode.ResolveDatabaseFile(AppServices.Current.Paths.DataHome);
            var store = new GenshinBeyondGachaStore(new LunarWardDatabase(file));
            const long uid = 100000001;

            var items = new List<GenshinBeyondGachaItemEx>();
            var id = 1L;
            // 常驻颂愿（op_gacha_type 1000）：60 抽、第 41 抽与第 59 抽各出一个 4★
            for (var i = 1; i <= 60; i++)
            {
                var rank = i is 41 or 59 ? 4 : 3;
                items.Add(new GenshinBeyondGachaItemEx
                {
                    Uid = uid,
                    Id = id,
                    Region = "cn_gf01",
                    OpGachaType = 1000,
                    ScheduleId = 1,
                    ItemType = "材料",
                    ItemId = 10000 + i,
                    ItemName = rank == 4 ? $"绥愿之结 {i}" : $"绮刻之楔 {i}",
                    RankType = rank,
                    IsUp = 0,
                    Time = new DateTime(2026, 10, 3, 12, 0, 0).AddMinutes(i * 7),
                });
                id++;
            }
            // 活动颂愿（op_gacha_type 2000）：40 抽、第 31 抽出 5★、第 12 抽出 4★
            for (var i = 1; i <= 40; i++)
            {
                var rank = i switch { 31 => 5, 12 => 4, _ => 3 };
                items.Add(new GenshinBeyondGachaItemEx
                {
                    Uid = uid,
                    Id = id,
                    Region = "cn_gf01",
                    OpGachaType = 2000,
                    ScheduleId = 2,
                    ItemType = "角色",
                    ItemId = 20000 + i,
                    ItemName = rank switch { 5 => "颂愿角色", 4 => $"颂愿伙伴 {i}", _ => $"驱动盘 {i}" },
                    RankType = rank,
                    IsUp = rank == 5 ? 1 : 0,
                    Time = new DateTime(2026, 10, 12, 20, 0, 0).AddMinutes(i * 5),
                });
                id++;
            }
            store.InsertItems(items);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"BEYOND_SEED_FAILED {ex.Message}");
        }
    }
}
