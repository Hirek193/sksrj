using System.ComponentModel;
using Avalonia.Controls;
using Sksrj.App.ViewModels;

namespace Sksrj.App.Views;

public partial class MainWindow : Window
{
    private GridLength _previewWidth = new(620);

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.PropertyChanged += OnVmChanged;
        };
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.PreviewVisible) || sender is not MainViewModel vm) return;
        var col = ContentGrid.ColumnDefinitions[3];
        if (vm.PreviewVisible) col.Width = _previewWidth;
        else { _previewWidth = col.Width; col.Width = new GridLength(0); }
    }
}
