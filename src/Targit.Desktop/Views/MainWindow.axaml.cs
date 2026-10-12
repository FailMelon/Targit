using Avalonia.Controls;
using Dock.Model.Core;
using Dock.Serializer;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Targit.Desktop.Docking;
using Targit.Desktop.ViewModels;

namespace Targit.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly TargitDockFactory _factory = new();
    private readonly DockSerializer _serializer = new();
    private readonly string _layoutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Targit", "workspace.json");

    public MainWindow()
    {
        InitializeComponent();
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        WorkspaceDock.Factory = _factory;
        WorkspaceDock.Layout = layout;
        Opened += (_, _) => RestoreLayout();
        Closing += (_, _) => SaveLayout();
    }

    private void RestoreLayout()
    {
        if (!File.Exists(_layoutPath)) return;
        try
        {
            using var stream = File.OpenRead(_layoutPath);
            var layout = _serializer.Load<IDock?>(stream);
            if (layout is null) return;
            WorkspaceDock.Layout = layout;
            _factory.InitLayout(layout);
        }
        catch (Exception)
        {
            // Corrupt or incompatible layouts must never prevent the app from opening.
            // The default workspace remains available for this session.
        }
    }

    private void SaveLayout()
    {
        if (WorkspaceDock.Layout is not { } layout) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_layoutPath)!);
            var temporary = _layoutPath + ".tmp";
            using (var stream = File.Create(temporary)) _serializer.Save(stream, layout);
            File.Move(temporary, _layoutPath, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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
