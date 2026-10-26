using Avalonia.Controls;

namespace LunarWard.Views;

public partial class AccountWindow : Window
{
    public AccountWindow() => InitializeComponent();


    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
