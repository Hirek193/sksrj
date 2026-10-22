using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Sksrj.App.ViewModels;
using Sksrj.App.Views;

namespace Sksrj.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var vm = new MainViewModel(new Services.DialogService(window));
            window.DataContext = vm;
            desktop.MainWindow = window;
            var arg = desktop.Args?.FirstOrDefault(a => File.Exists(a));
            if (arg != null) vm.OpenPath(arg);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
