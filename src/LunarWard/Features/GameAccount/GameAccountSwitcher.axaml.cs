using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Core;
using LunarWard.Language;
using LunarWard.Services;
using LunarWard.Views.Starward.Controls;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LunarWard.Features.GameAccount;

/// <summary>
/// 账号切换器。<b>移植自</b>
/// <c>Starward/src/Starward/Features/GameAccount/GameAccountSwitcher.xaml(.cs)</c>。
/// </summary>
/// <remarks>
/// 控件替换：WinUI <c>AutoSuggestBox</c> → Avalonia <c>AutoCompleteBox</c>，
/// <c>ContentDialog</c> → <see cref="StarwardContentDialog"/> + <see cref="DialogService"/>。
/// 其余属性、命令、日志文案与上游一致。见 <c>docs/09-LINUX-DIVERGENCES.md</c>。
/// </remarks>
public sealed partial class GameAccountSwitcher : UserControl, INotifyPropertyChanged
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


    private readonly ILogger<GameAccountService> _logger = AppConfig.GetLogger<GameAccountService>();


    private readonly GameAccountService _gameAccountService = AppConfig.GetService<GameAccountService>();


    public GameAccountSwitcher()
    {
        this.InitializeComponent();
        this.Loaded += GameAccountSwitcher_Loaded;
        this.Unloaded += GameAccountSwitcher_Unloaded;
    }



    private void GameAccountSwitcher_Loaded(object? sender, RoutedEventArgs e)
    {
        UpdateGameAccount();
    }


    private void GameAccountSwitcher_Unloaded(object? sender, RoutedEventArgs e)
    {
        SelectGameAccount = null;
        GameAccountList = null!;
        suggestionUids = null!;
    }



    public static readonly StyledProperty<GameBiz> CurrentGameBizProperty =
        AvaloniaProperty.Register<GameAccountSwitcher, GameBiz>(nameof(CurrentGameBiz));

    public GameBiz CurrentGameBiz
    {
        get => GetValue(CurrentGameBizProperty);
        set => SetValue(CurrentGameBizProperty, value);
    }



    private List<GameAccount> _gameAccountList;

    public List<GameAccount> GameAccountList
    {
        get => _gameAccountList;
        set => SetProperty(ref _gameAccountList, value);
    }


    private GameAccount? _selectGameAccount;

    public GameAccount? SelectGameAccount
    {
        get => _selectGameAccount;
        set
        {
            if (SetProperty(ref _selectGameAccount, value) && value is not null)
            {
                CanChangeGameAccount = true;
            }
        }
    }


    private bool _canChangeGameAccount;

    public bool CanChangeGameAccount
    {
        get => _canChangeGameAccount;
        set => SetProperty(ref _canChangeGameAccount, value);
    }


    public void UpdateGameAccount()
    {
        try
        {
            GameAccountList = _gameAccountService.GetGameAccounts(CurrentGameBiz);
            SelectGameAccount = GameAccountList.FirstOrDefault();
            CanChangeGameAccount = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot get game account ({biz})", CurrentGameBiz);
        }
    }


    [RelayCommand]
    private void ChangeGameAccount()
    {
        try
        {
            if (SelectGameAccount is not null)
            {
                _gameAccountService.ChangeGameAccount(SelectGameAccount);
                CanChangeGameAccount = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot change game {biz} account to {name}", CurrentGameBiz, SelectGameAccount?.Name);
        }
    }


    [RelayCommand]
    private async Task SaveGameAccountAsync()
    {
        try
        {
            if (SelectGameAccount is not null)
            {
                var acc = SelectGameAccount;
                if (GameAccountList.FirstOrDefault(x => x.SHA256 != acc.SHA256 && x.Uid == acc.Uid) is GameAccount lacc)
                {
                    bool primary = false;
                    var dialog = new StarwardContentDialog
                    {
                        Title = Lang.Common_Attention,
                        Content = new Avalonia.Controls.TextBlock
                        {
                            Text = string.Format(Lang.LauncherPage_AccountSaveNew, acc.Uid),
                            TextWrapping = TextWrapping.Wrap,
                        },
                        PrimaryButtonText = Lang.LauncherPage_Replace,
                        SecondaryButtonText = Lang.LauncherPage_SaveNew,
                    };
                    dialog.PrimaryButtonClicked += (_, _) => primary = true;
                    await DialogService.ShowAsync(dialog, this);
                    if (primary)
                    {
                        GameAccountList.Remove(lacc);
                        _gameAccountService.DeleteGameAccount(lacc);
                    }
                }
                SelectGameAccount.Time = DateTime.Now;
                _gameAccountService.SaveGameAccount(SelectGameAccount);
                FontIcon_SaveGameAccount.Text = "\uE8FB";
                await Task.Delay(3000);
                FontIcon_SaveGameAccount.Text = "\uE74E";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save game account");
        }
    }


    [RelayCommand]
    private void DeleteGameAccount()
    {
        try
        {
            if (SelectGameAccount is not null)
            {
                _gameAccountService.DeleteGameAccount(SelectGameAccount);
                UpdateGameAccount();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete game account");
        }
    }



    private List<string>? suggestionUids;


    private void AutoSuggestBox_Uid_GotFocus(object? sender, FocusChangedEventArgs e)
    {
        try
        {
            suggestionUids = _gameAccountService.GetSuggestionUids(CurrentGameBiz).Select(x => x.ToString()).ToList();
            // 平台替换：WinUI AutoSuggestBox 需要手动喂建议项；Avalonia AutoCompleteBox 给 ItemsSource 后自己过滤
            AutoSuggestBox_Uid.ItemsSource = suggestionUids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get suggestion uids");
        }
    }


    private void AutoSuggestBox_Uid_TextInput(object? sender, TextInputEventArgs e)
    {
        // 平台替换：WinUI 用 InputScope(Number) + BeforeTextChanging 拦非数字；
        // Avalonia 没有 InputScope，这里在 TextInput 阶段拦。
        if (!string.IsNullOrEmpty(e.Text) && !e.Text.All(char.IsDigit))
        {
            e.Handled = true;
        }
    }

}
