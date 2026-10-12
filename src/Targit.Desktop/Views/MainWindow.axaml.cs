using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Targit.Desktop.Docking;
using Targit.Desktop.ViewModels;

namespace Targit.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly TargitDockFactory _factory = new();

    public MainWindow()
    {
        InitializeComponent();
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        WorkspaceDock.Factory = _factory;
        WorkspaceDock.Layout = layout;
    }

    private async void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Git repository",
            AllowMultiple = false
        });
        if (folders.Count == 0) return;
        vm.RepositoryPath = folders[0].Path.LocalPath;
        await vm.AddRepositoryCommand.ExecuteAsync(null);
    }
}
