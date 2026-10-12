using Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Dock.Serializer;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Targit.Desktop.Docking;
using Targit.Desktop.ViewModels;
using Targit.Infrastructure;

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

    private DocumentDock? FindDocuments()
    {
        if (WorkspaceDock.Layout is not { } root) return null;
        return FindDocumentsRecursive(root);
    }

    private static DocumentDock? FindDocumentsRecursive(IDockable dockable)
    {
        if (dockable is DocumentDock documents) return documents;
        if (dockable is IDock dock && dock.VisibleDockables is { } children)
        {
            foreach (var child in children)
            {
                var found = FindDocumentsRecursive(child);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private void OpenTab_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var documents = FindDocuments();
        if (documents?.VisibleDockables is not { } tabs) return;
        var title = vm.NewTab;
        if (!vm.AvailableTabs.Contains(title)) return;
        var existing = tabs.OfType<Document>().FirstOrDefault(d => d.Id == title.ToLowerInvariant());
        if (existing is null)
        {
            existing = new Document { Id = title.ToLowerInvariant(), Title = title };
            tabs.Add(existing);
        }
        documents.ActiveDockable = existing;
        vm.SelectedTab = title;
        if (!vm.Tabs.Contains(title)) vm.Tabs.Add(title);
    }

    private void CloseTab_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var documents = FindDocuments();
        if (documents?.VisibleDockables is not { } tabs || tabs.Count <= 1) return;
        var selected = documents.ActiveDockable as Document;
        if (selected is null) return;
        tabs.Remove(selected);
        documents.ActiveDockable = tabs.OfType<Document>().FirstOrDefault();
        vm.Tabs.Remove(selected.Title ?? "");
        vm.SelectedTab = documents.ActiveDockable?.Title ?? "History";
    }

    private string NamedLayoutPath()
    {
        var name = (DataContext as MainWindowViewModel)?.SelectedLayout ?? "Standard";
        return Path.Combine(Path.GetDirectoryName(_layoutPath)!, $"layout-{name.Replace(' ', '-')}.json");
    }

    private void SaveLayout_Click(object? sender, RoutedEventArgs e) => SaveLayout(NamedLayoutPath());
    private void LoadLayout_Click(object? sender, RoutedEventArgs e) => RestoreLayout(NamedLayoutPath());
    private void ResetLayout_Click(object? sender, RoutedEventArgs e)
    {
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        WorkspaceDock.Layout = layout;
        SaveLayout();
    }

    private void RestoreLayout() => RestoreLayout(_layoutPath);

    private void RestoreLayout(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            using var stream = File.OpenRead(path);
            var layout = _serializer.Load<IDock?>(stream);
            if (layout is null) return;
            _factory.InitLayout(layout);
            WorkspaceDock.Layout = layout;
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Restore dock layout");
            // Corrupt or incompatible layouts must never prevent the app from opening.
            // The default workspace remains available for this session.
        }
    }

    private void SaveLayout() => SaveLayout(_layoutPath);

    private void SaveLayout(string path)
    {
        if (WorkspaceDock.Layout is not { } layout) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            using (var stream = File.Create(temporary)) _serializer.Save(stream, layout);
            File.Move(temporary, path, true);
        }
        catch (IOException e) { FileLog.Error(e, "Save dock layout"); }
        catch (UnauthorizedAccessException e) { FileLog.Error(e, "Save dock layout"); }
        catch (Exception e) { FileLog.Error(e, "Serialize dock layout"); }
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
