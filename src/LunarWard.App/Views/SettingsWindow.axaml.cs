using Avalonia.Controls;
using LunarWard.App.ViewModels;

namespace LunarWard.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();


    /// <summary>保存按钮：写回 profile 后关闭。</summary>
    private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        (DataContext as SettingsViewModel)?.Apply();
        Close();
    }
}
