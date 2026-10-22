using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Sksrj.App.ViewModels;
using Sksrj.Core.Dictionaries;

namespace Sksrj.App.Views;

public partial class PointsView : UserControl
{
    public PointsView() => InitializeComponent();

    private MainViewModel? Vm => DataContext as MainViewModel;

    private static CodeEntry? Take(object? sender)
    {
        if (sender is not ListBox lb || lb.SelectedItem is not CodeEntry e) return null;
        lb.SelectedItem = null;
        // zamknij flyout, w którym znajduje się lista
        if (lb.FindAncestorOfType<FlyoutPresenter>()?.Parent is Popup popup) popup.Close();
        return e;
    }

    private void OnTypePicked(object? sender, SelectionChangedEventArgs e) { if (Take(sender) is { } c) Vm?.SetPointTypeCommand.Execute(c); }
    private void OnStopKindPicked(object? sender, SelectionChangedEventArgs e) { if (Take(sender) is { } c) Vm?.InsertStopKindCommand.Execute(c); }
    private void OnAbbreviationPicked(object? sender, SelectionChangedEventArgs e) { if (Take(sender) is { } c) Vm?.InsertAbbreviationCommand.Execute(c); }
    private void OnBrakePicked(object? sender, SelectionChangedEventArgs e) { if (Take(sender) is { } c) Vm?.SetBrakeTypeCommand.Execute(c); }
}
