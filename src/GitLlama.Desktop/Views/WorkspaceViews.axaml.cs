using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using GitLlama.Core;
using GitLlama.Desktop.ViewModels;

namespace GitLlama.Desktop.Views;

public partial class HistoryView : UserControl
{
    private MainWindowViewModel? _historyWorkspace;

    public HistoryView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => ConnectHistoryNavigation();
        AttachedToVisualTree += (_, _) => ConnectHistoryNavigation();
        DetachedFromVisualTree += (_, _) =>
        {
            if (_historyWorkspace is not null)
            {
                _historyWorkspace.PropertyChanged -= HistoryNavigationChanged;
            }
        };
    }

    private void ConnectHistoryNavigation()
    {
        if (_historyWorkspace is not null)
        {
            _historyWorkspace.PropertyChanged -= HistoryNavigationChanged;
        }

        _historyWorkspace = DataContext as MainWindowViewModel;
        if (_historyWorkspace is not null)
        {
            _historyWorkspace.PropertyChanged += HistoryNavigationChanged;
        }

        ScrollToHistorySelection();
    }

    private void HistoryNavigationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.HistoryRevealRevision))
        {
            ScrollToHistorySelection();
        }
    }

    private void ScrollToHistorySelection() =>
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () =>
            {
                if (_historyWorkspace?.SelectedHistoryRow is { } row)
                {
                    this.FindControl<ListBox>("HistoryList")?.ScrollIntoView(row);
                }
            },
            Avalonia.Threading.DispatcherPriority.Loaded
        );

    private void ClearSearch_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.HistorySearch = vm.HistoryAuthorFilter = "";
            vm.HistorySince = vm.HistoryUntil = null;
        }
    }

    private void OpenFolder_Click(object? sender, RoutedEventArgs e) =>
        (TopLevel.GetTopLevel(this) as MainWindow)?.OpenRepositoryFolder();
}

public partial class NavigationView : UserControl
{
    public NavigationView() => AvaloniaXamlLoader.Load(this);

    private void OpenFolder_Click(object? sender, RoutedEventArgs e) =>
        (TopLevel.GetTopLevel(this) as MainWindow)?.OpenRepositoryFolder();

    private void PullRequests_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.DockDocument?.Invoke("Pull Requests");

    private void Issues_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as MainWindowViewModel)?.DockDocument?.Invoke("Issues");

    private void Worktrees_Click(object? sender, RoutedEventArgs e) =>
        (TopLevel.GetTopLevel(this) as MainWindow)?.ShowDocument("Worktrees");

    private void Tags_Click(object? sender, RoutedEventArgs e) =>
        (TopLevel.GetTopLevel(this) as MainWindow)?.ShowDocument("Tags");

    private void Branches_Click(object? sender, RoutedEventArgs e) =>
        (TopLevel.GetTopLevel(this) as MainWindow)?.ShowDocument("Branches");

    private void Repository_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Clearing a filtered list must not close the active repository.
        if (DataContext is MainWindowViewModel vm && e.AddedItems.OfType<RepositoryInfo>().FirstOrDefault() is { } repo)
        {
            vm.SelectedRepository = repo;
        }
    }

    private void Branch_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && e.AddedItems.OfType<string>().FirstOrDefault() is { } branch)
        {
            vm.SelectedBranchLine = branch;
        }
    }
}

public partial class InspectorView : UserControl
{
    public InspectorView() => AvaloniaXamlLoader.Load(this);

    private void StagedFile_SelectionChanged(object? sender, SelectionChangedEventArgs e) => SelectWorkingFile(e, true);

    private void UnstagedFile_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        SelectWorkingFile(e, false);

    private void SelectWorkingFile(SelectionChangedEventArgs e, bool staged)
    {
        if (
            DataContext is MainWindowViewModel vm
            && !vm.IsUpdatingContextSnapshot
            && e.AddedItems.OfType<WorkingFileRow>().FirstOrDefault() is { } file
        )
        {
            vm.ReviewStagedPatch = staged;
            vm.SelectedWorkingFile = file;
        }
    }

    private void WorkingFileContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListBox list || DataContext is not MainWindowViewModel { UsesLfs: true } vm)
        {
            return;
        }

        var file =
            (e.Source as Control)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()?.DataContext
                as WorkingFileRow
            ?? list.SelectedItem as WorkingFileRow;
        if (file is null)
        {
            return;
        }

        list.ContextMenu = new ContextMenu { ItemsSource = LfsLockMenu.Items(this, vm, file.Path).ToArray() };
        list.ContextMenu.Open(list);
        e.Handled = true;
    }

    private async void StageCheckbox_Click(object? sender, RoutedEventArgs e)
    {
        if (
            DataContext is not MainWindowViewModel vm
            || sender is not CheckBox { DataContext: WorkingFileRow file } checkbox
        )
        {
            return;
        }

        var unstage = checkbox.Tag as string == "unstage";
        checkbox.IsChecked = unstage;
        vm.SelectedWorkingFile = file;
        if (unstage)
        {
            await vm.UnstageSelectedCommand.ExecuteAsync(null);
        }
        else
        {
            await vm.StageSelectedCommand.ExecuteAsync(null);
        }

        vm.ReviewStagedPatch = vm.SelectedWorkingFile?.IsStaged == true;
        e.Handled = true;
    }
}

public partial class InspectorView : UserControl
{
    private async void ExportPatch_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || TopLevel.GetTopLevel(this) is not { } window)
        {
            return;
        }

        try
        {
            var file = await window.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Export selected file patch",
                    SuggestedFileName = "changes.patch",
                    FileTypeChoices = [new FilePickerFileType("Git patch") { Patterns = ["*.patch"] }],
                }
            );
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            await writer.WriteAsync(vm.ReviewStagedPatch ? vm.StagedDiffText : vm.UnstagedDiffText);
            vm.Status = "Patch exported.";
        }
        catch (Exception exception)
        {
            vm.Status = exception.Message;
        }
    }
}
