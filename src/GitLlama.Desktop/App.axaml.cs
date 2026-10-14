using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GitLlama.Application;
using GitLlama.Core;
using GitLlama.Desktop.Docking;
using GitLlama.Desktop.ViewModels;
using GitLlama.Desktop.Views;
using GitLlama.Git;
using GitLlama.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GitLlama.Desktop;

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
