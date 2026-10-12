using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Targit.Application;
using Targit.Core;
using Targit.Git;
using Targit.Infrastructure;
using Targit.Desktop.ViewModels;
using Targit.Desktop.Views;
namespace Targit.Desktop;
public partial class App : Application
{
    private ServiceProvider? _provider;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRepositoryReader, LibGitRepositoryReader>();
        services.AddSingleton<RepositoryWorkspace>();
        services.AddSingleton<JsonSettingsStore>();
        services.AddSingleton<MainWindowViewModel>();
        _provider = services.BuildServiceProvider();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = _provider.GetRequiredService<MainWindowViewModel>() };
            desktop.Exit += (_, _) => _provider.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}