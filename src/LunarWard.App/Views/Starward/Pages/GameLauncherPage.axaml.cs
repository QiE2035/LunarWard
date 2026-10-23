using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Pages;

public partial class GameLauncherPage : StarwardPage
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    private GameLauncherPageViewModel? _viewModel;


    public GameLauncherPage(PageContext context) : base(context)
    {
        InitializeComponent();
        _viewModel = new GameLauncherPageViewModel(context);
        DataContext = _viewModel;
    }


    protected override void DisposeViewModel(PageContext context)
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }


    public override void OnNavigatedTo()
    {
        _clock.Tick += OnClockTick;
        _clock.Start();
    }


    public override void OnNavigatedFrom()
    {
        _clock.Stop();
        _clock.Tick -= OnClockTick;
        base.OnNavigatedFrom();
    }


    private void OnClockTick(object? sender, EventArgs e) => _viewModel?.TickRunningTime();


    private void OnBannerPointerEntered(object? sender, PointerEventArgs e) => _viewModel?.OnBannerPointerEnter();


    private void OnBannerPointerExited(object? sender, PointerEventArgs e) => _viewModel?.OnBannerPointerExit();


    private void OnBannerTapped(object? sender, TappedEventArgs e) => _viewModel?.OpenBannerLinkCommand.Execute(null);


    private void OnBackgroundBarPointerEntered(object? sender, PointerEventArgs e) => _viewModel?.OnBackgroundBarPointerEnter();


    private void OnBackgroundBarPointerExited(object? sender, PointerEventArgs e) => _viewModel?.OnBackgroundBarPointerExit();
}
