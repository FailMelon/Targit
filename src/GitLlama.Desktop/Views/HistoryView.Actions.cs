using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using GitLlama.Desktop.ViewModels;

namespace GitLlama.Desktop.Views;

public partial class HistoryView
{
    private void CommitContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListBox list || DataContext is not MainWindowViewModel vm) return;
        if ((e.Source as Control)?.DataContext is HistoryRow row) vm.SelectedHistoryRow = row;
        if (vm.SelectedHistoryRow is not { } selected) return;
        var items = new List<MenuItem>();
        void Add(string name, Func<Task> action)
        {
            var item = new MenuItem { Header = name };
            item.Click += async (_, _) => await action(); items.Add(item);
        }
        Add("Dock History in main window", () => { vm.DockDocument?.Invoke("History"); return Task.CompletedTask; });
        Add("Create annotated tag here…", () => TagCreationDialog.ShowAsync(this, vm, selected.Sha));
        Add("Copy commit SHA", async () => { if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(selected.Sha); });
        Add("Create branch here…", () => PromptAsync("Create branch at " + selected.ShortSha, "New branch name", "Create branch", "", value => vm.BranchFromCommitName = value, vm.CreateBranchFromSelectedCommitCommand));
        Add("Create tag here…", () => PromptAsync("Tag " + selected.ShortSha, "New lightweight tag name", "Create tag", "", value => { vm.NewTagName = value; vm.ContextTagTarget = selected.Sha; }, vm.CreateContextTagCommand));
        Add("Cherry-pick commit…", () => ConfirmAsync("Cherry-pick " + selected.ShortSha, "Replay this commit onto the current branch. Conflicts remain available for resolution.", vm.CherryPickSelectedCommitCommand));
        Add("Revert commit…", () => ConfirmAsync("Revert " + selected.ShortSha, "Create a new commit that reverses this commit. Current history is retained.", vm.RevertSelectedCommitCommand));
        Add("Reset branch (soft)…", () => PromptAsync("Soft reset to " + selected.ShortSha, "Move HEAD; keep staged and working changes.", "Reset", "RESET " + selected.ShortSha, value => vm.SoftResetConfirmation = value, vm.SoftResetToSelectedCommitCommand));
        Add("Reset branch (mixed)…", () => PromptAsync("Mixed reset to " + selected.ShortSha, "Move HEAD and reset the index; keep working files.", "Reset", "MIXED " + selected.ShortSha, value => vm.MixedResetConfirmation = value, vm.MixedResetToSelectedCommitCommand));
        Add("Reset branch (hard)…", () => PromptAsync("Hard reset to " + selected.ShortSha, "Move HEAD and overwrite tracked working files. Uncommitted tracked changes are lost.", "Reset", "HARD RESET " + selected.ShortSha, value => vm.HardResetConfirmation = value, vm.HardResetToSelectedCommitCommand));
        list.ContextMenu = new ContextMenu { ItemsSource = items }; list.ContextMenu.Open(list); e.Handled = true;
    }
    private async Task ConfirmAsync(string title, string description, IAsyncRelayCommand command)
    {
        await PromptAsync(title, description, "Continue", "CONFIRM", _ => { }, command);
    }
    private async Task PromptAsync(string title, string description, string action, string expected, Action<string> setValue, IAsyncRelayCommand command)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner || DataContext is not MainWindowViewModel vm) return;
        var sha = vm.SelectedHistoryRow?.Sha; var repository = vm.SelectedRepository?.Path;
        var input = new TextBox { Watermark = expected.Length > 0 ? "Type " + expected : "Branch name" };
        var button = new Button { Content = action, HorizontalAlignment = HorizontalAlignment.Right }; button.Classes.Add("workspace");
        var dialog = new Window { Title = title, Width = 520, Height = 210, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 12, Children = { new TextBlock { Text = description, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, input, button } };
        button.Click += async (_, _) =>
        {
            if (vm.SelectedHistoryRow?.Sha != sha || vm.SelectedRepository?.Path != repository) { vm.Status = "Repository or commit selection changed. Review the action again."; dialog.Close(); return; }
            if (expected.Length > 0 && input.Text != expected) return;
            setValue(input.Text ?? ""); dialog.Close(); await command.ExecuteAsync(null);
        };
        await dialog.ShowDialog(owner);
    }
}
