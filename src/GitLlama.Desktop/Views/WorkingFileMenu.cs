using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitLlama.Desktop.ViewModels;
using GitLlama.Git;

namespace GitLlama.Desktop.Views;

/// <summary>The right-click menu for a file in the working changes lists.</summary>
public static class WorkingFileMenu
{
    /// <param name="inStagedList">Whether the menu was opened from the staged list rather than the unstaged one.</param>
    public static object[] Items(Control owner, MainWindowViewModel vm, WorkingFileRow file, bool inStagedList)
    {
        var items = new List<object>();
        MenuItem Item(string header, Func<Task> action, bool enabled = true)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            item.Click += async (_, _) => await action();
            return item;
        }

        var untracked = file.Status == "Added" && !file.IsStaged;
        var exists = vm.FullPath(file.Path) is { } full && File.Exists(full);

        // Change the file.
        items.Add(
            inStagedList
                ? Item("Unstage", () => vm.SetFileStagedAsync(file, stage: false))
                : Item("Stage", () => vm.SetFileStagedAsync(file, stage: true))
        );
        if (!inStagedList && file.Status != "Conflict")
        {
            items.Add(
                untracked
                    ? Item("Discard new file…", () => DeleteAsync(owner, vm, file, discarding: true))
                    : Item("Discard changes…", () => DiscardAsync(owner, vm, file))
            );
        }

        var ignore = new MenuItem { Header = "Ignore" };
        ignore.ItemsSource = GitIgnoreService
            .Suggestions(file.Path)
            .Select(suggestion =>
            {
                var item = Item(suggestion.Label, () => vm.IgnoreAsync(suggestion.Pattern, tracked: !untracked));
                ToolTip.SetTip(item, "Adds " + suggestion.Pattern + " to .gitignore");
                return item;
            })
            .ToArray();
        items.Add(ignore);
        items.Add(Item("Stash file", () => vm.StashFileAsync(file.Path), file.Status != "Conflict"));
        items.Add(new Separator());

        // Look at the file.
        items.Add(Item("File history", () => vm.ReviewFileAsync(file, blame: false), !untracked));
        items.Add(Item("File blame", () => vm.ReviewFileAsync(file, blame: true), !untracked && exists));
        items.Add(new Separator());
        items.Add(Item("Open file", () => OpenAsync(owner, vm, file.Path), exists));
        items.Add(Item("Show in folder", () => ShowInFolder(vm, file.Path)));
        items.Add(new Separator());

        // Copy and export.
        items.Add(Item("Copy file path", () => CopyAsync(owner, file.Path)));
        items.Add(Item("Copy full path", () => CopyAsync(owner, vm.FullPath(file.Path) ?? file.Path)));
        items.Add(Item("Create patch from file changes…", () => ExportPatchAsync(owner, vm, file, inStagedList)));
        items.Add(new Separator());
        items.Add(Item("Delete file…", () => DeleteAsync(owner, vm, file, discarding: false), exists));

        if (vm.UsesLfs)
        {
            items.Add(new Separator());
            items.AddRange(LfsLockMenu.Items(owner, vm, file.Path));
        }

        return items.ToArray();
    }

    private static async Task DiscardAsync(Control owner, MainWindowViewModel vm, WorkingFileRow file)
    {
        if (
            TopLevel.GetTopLevel(owner) is Window window
            && await ConfirmDialog.ShowAsync(
                window,
                $"Discard changes to {file.FileName}?",
                $"Unstaged edits to {file.Path} are thrown away and the file goes back to its staged or committed "
                    + "content. This can't be undone.",
                "Discard changes",
                destructive: true
            )
        )
        {
            await vm.DiscardFileAsync(file);
        }
    }

    private static async Task DeleteAsync(Control owner, MainWindowViewModel vm, WorkingFileRow file, bool discarding)
    {
        var untracked = file.Status == "Added" && !file.IsStaged;
        if (
            TopLevel.GetTopLevel(owner) is Window window
            && await ConfirmDialog.ShowAsync(
                window,
                $"Delete {file.FileName}?",
                untracked
                    ? $"{file.Path} isn't in Git yet, so deleting it can't be undone."
                    : $"{file.Path} is deleted from your working folder. Git still has the committed version, so "
                        + "you can bring it back by discarding the deletion.",
                discarding ? "Discard new file" : "Delete file",
                destructive: true
            )
        )
        {
            await vm.DeleteFileAsync(file.Path);
        }
    }

    private static async Task OpenAsync(Control owner, MainWindowViewModel vm, string path)
    {
        if (vm.FullPath(path) is { } full && TopLevel.GetTopLevel(owner)?.Launcher is { } launcher)
        {
            if (!await launcher.LaunchFileInfoAsync(new FileInfo(full)))
            {
                vm.Status = "No program is set up to open " + System.IO.Path.GetFileName(full) + ".";
            }
        }
    }

    private static Task ShowInFolder(MainWindowViewModel vm, string path)
    {
        if (vm.FullPath(path) is not { } full)
        {
            return Task.CompletedTask;
        }

        try
        {
            var folder = System.IO.Path.GetDirectoryName(full)!;
            var start =
                OperatingSystem.IsWindows() && File.Exists(full)
                    ? new ProcessStartInfo("explorer.exe", $"/select,\"{full}\"")
                : OperatingSystem.IsMacOS() && File.Exists(full) ? new ProcessStartInfo("open", ["-R", full])
                : OperatingSystem.IsWindows() ? new ProcessStartInfo("explorer.exe", $"\"{folder}\"")
                : OperatingSystem.IsMacOS() ? new ProcessStartInfo("open", [folder])
                : new ProcessStartInfo("xdg-open", [folder]);
            start.UseShellExecute = false;
            Process.Start(start)?.Dispose();
        }
        catch (Exception e)
        {
            vm.Status = "Couldn't open the folder: " + e.Message;
        }

        return Task.CompletedTask;
    }

    private static async Task CopyAsync(Control owner, string text)
    {
        if (TopLevel.GetTopLevel(owner)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private static async Task ExportPatchAsync(Control owner, MainWindowViewModel vm, WorkingFileRow file, bool staged)
    {
        if (TopLevel.GetTopLevel(owner) is not { } top)
        {
            return;
        }

        try
        {
            var patch = await vm.FilePatchAsync(file.Path, staged);
            if (patch is null)
            {
                vm.Status = $"{file.Path} has no {(staged ? "staged" : "unstaged")} changes to put in a patch.";
                return;
            }

            var target = await top.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Create patch from file changes",
                    SuggestedFileName = file.FileName + ".patch",
                    FileTypeChoices = [new FilePickerFileType("Git patch") { Patterns = ["*.patch"] }],
                }
            );
            if (target is null)
            {
                return;
            }

            await using var stream = await target.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            await writer.WriteAsync(patch);
            vm.Status = $"Saved a patch of {file.Path}.";
        }
        catch (Exception e)
        {
            vm.Status = e.Message;
        }
    }
}
