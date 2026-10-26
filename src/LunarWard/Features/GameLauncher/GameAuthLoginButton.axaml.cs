using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Core.HoYoPlay;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 通行证登录按钮（吸附在启动按钮左侧）。<b>移植自</b>
/// <c>Starward/src/Starward/Features/GameLauncher/GameAuthLoginButton.xaml(.cs)</c>。
/// </summary>
/// <remarks>
/// 控件替换：WinUI <c>ToggleSwitch.IsOn</c> → Avalonia <c>ToggleSwitch.IsChecked</c>；
/// <c>ThemeResource DateTimePickerFlyoutButtonStyle</c> 在 Avalonia 无对应，用默认 Button 样式。
/// 其余属性、命令、文案与上游一致。
/// </remarks>
public sealed partial class GameAuthLoginButton : UserControl, INotifyPropertyChanged
{

    public new event PropertyChangedEventHandler? PropertyChanged;


    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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



    private readonly ILogger<GameAuthLoginButton> _logger = AppConfig.GetLogger<GameAuthLoginButton>();


    private readonly GameAuthLoginService _gameAuthLoginService = AppConfig.GetService<GameAuthLoginService>();


    public GameAuthLoginButton()
    {
        this.InitializeComponent();
        this.IsVisible = false;
    }



    public GameId CurrentGameId { get; set; }


    private long? _hyperionAid;

    public long? HyperionAid
    {
        get => _hyperionAid;
        set => SetProperty(ref _hyperionAid, value);
    }


    private string? _errorMessage;

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }


    private bool _enableLoginAuthTicket = AppConfig.EnableLoginAuthTicket ?? false;

    public bool EnableLoginAuthTicket
    {
        get => _enableLoginAuthTicket;
        set
        {
            if (SetProperty(ref _enableLoginAuthTicket, value))
            {
                AppConfig.EnableLoginAuthTicket = value;
                AppConfig.SaveConfiguration();
            }
        }
    }


    private void Button_GameAuthLogin_Loaded(object? sender, RoutedEventArgs e)
    {
        InitializeGameAuthLoginCommand.Execute(null);
    }



    [RelayCommand]
    private async Task InitializeGameAuthLoginAsync()
    {
        try
        {
            if (CurrentGameId is null || CurrentGameId.GameBiz.Server is not "cn")
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(AppConfig.stoken) || string.IsNullOrWhiteSpace(AppConfig.mid))
            {
                return;
            }
            this.IsVisible = true;
            ErrorMessage = null;
            HyperionAid = await _gameAuthLoginService.GetHyperionAidAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InitializeGameAuthLogin");
            ErrorMessage = ex.Message;
        }
    }


}
