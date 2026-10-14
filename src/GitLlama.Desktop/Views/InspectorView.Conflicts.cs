using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GitLlama.Desktop.ViewModels;
using GitLlama.Git;

namespace GitLlama.Desktop.Views;

/// <summary>The working changes inspector's merge mode: conflicted files open in the resolver.</summary>
public partial class InspectorView
{
    private async void ConflictedFile_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (
            DataContext is MainWindowViewModel vm
            && !vm.IsUpdatingContextSnapshot
            && e.AddedItems.OfType<WorkingFileRow>().FirstOrDefault() is { } file
            && vm.ConflictResolver?.Path != file.Path
        )
        {
            await vm.OpenConflictFileCommand.ExecuteAsync(file.Path);
        }
    }

    private async void ConflictedFile_Tapped(object? sender, TappedEventArgs e)
    {
        // Clicking the file that is already selected brings the resolver back after viewing something else.
        if (
            DataContext is MainWindowViewModel vm
            && (e.Source as Control)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()?.DataContext
                is WorkingFileRow file
            && (!vm.IsConflictResolver || vm.ConflictResolver?.Path != file.Path)
        )
        {
            await vm.OpenConflictFileCommand.ExecuteAsync(file.Path);
        }
    }

    private void ConflictedFileContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListBox list || DataContext is not MainWindowViewModel vm)
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

        MenuItem Item(string header, Func<Task> action)
        {
            var item = new MenuItem { Header = header };
            item.Click += async (_, _) => await action();
            return item;
        }

        list.ContextMenu = new ContextMenu
        {
            ItemsSource = new object[]
            {
                Item("Resolve conflicts…", () => vm.OpenConflictFileCommand.ExecuteAsync(file.Path)),
                Item("Use all of A (ours)", () => vm.UseConflictSideAsync(file.Path, GitConflictChoice.Ours)),
                Item("Use all of B (theirs)", () => vm.UseConflictSideAsync(file.Path, GitConflictChoice.Theirs)),
                new Separator(),
                Item(
                    "Copy path",
                    async () =>
                    {
                        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                        {
                            await clipboard.SetTextAsync(file.Path);
                        }
                    }
                ),
            },
        };
        list.ContextMenu.Open(list);
        e.Handled = true;
    }

    private void ResolvedFile_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (
            DataContext is MainWindowViewModel vm
            && !vm.IsUpdatingContextSnapshot
            && e.AddedItems.OfType<WorkingFileRow>().FirstOrDefault() is { } file
        )
        {
            vm.ReviewStagedPatch = file.IsStaged;
            vm.SelectedWorkingFile = file;
        }
    }
}
