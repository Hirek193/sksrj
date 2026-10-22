using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sksrj.App.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog() => InitializeComponent();

    public static async Task<bool> ShowAsync(Window owner, string title, string message, string ok, string cancel)
    {
        var d = new ConfirmDialog();
        d.TitleText.Text = title;
        d.MessageText.Text = message;
        d.OkButton.Content = ok;
        d.CancelButton.Content = cancel;
        return await d.ShowDialog<bool>(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
