using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Targit.Application;
using Targit.Core;
using Targit.Git;
using Targit.Infrastructure;
using Targit.Desktop.ViewModels;
using Targit.Desktop.Docking;
using Targit.Desktop.Views;
namespace Targit.Desktop;
public partial class App : Avalonia.Application
{
    private ServiceProvider? _provider;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRepositoryReader, LibGitRepositoryReader>();
        services.AddSingleton<IRepositoryOverviewReader, LibGitOverviewReader>();
        services.AddSingleton<GitWorkingTreeService>();
        services.AddSingleton<RepositoryWorkspace>();
        services.AddSingleton<RepositorySessionManager>();
        services.AddSingleton<JsonSettingsStore>();
        services.AddSingleton<MainWindowViewModel>();
        _provider = services.BuildServiceProvider();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = _provider.GetRequiredService<MainWindowViewModel>();
            DataTemplates.Add(new DockContentTemplate(vm));
            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.Exit += (_, _) => _provider.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}