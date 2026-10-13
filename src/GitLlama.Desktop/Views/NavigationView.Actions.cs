using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using GitLlama.Desktop.ViewModels;
using GitLlama.Git;

namespace GitLlama.Desktop.Views;

public partial class NavigationView
{
    private async void Branch_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox list || DataContext is not MainWindowViewModel vm) return;
        var item = (e.Source as Control)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        if (item?.DataContext is not string branch || !list.GetVisualDescendants().Contains(item)) return;
        e.Handled = true;
        await vm.RevealBranchHistoryCommand.ExecuteAsync(branch);
    }
    private async void Branch_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox list) return;
        var item = (e.Source as Control)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        if (item?.DataContext is not string branch || !list.GetVisualDescendants().Contains(item)) return;
        e.Handled = true;
        await CheckoutSidebarBranchAsync(branch);
    }

    private async Task CheckoutSidebarBranchAsync(string branch)
    {
        if (DataContext is not MainWindowViewModel vm || branch == vm.CurrentBranch || branch.EndsWith("/HEAD", StringComparison.Ordinal)) return;
        vm.SelectedBranchLine = branch;
        if (vm.Remotes.Any(r => branch.StartsWith(r.Name + "/", StringComparison.Ordinal)))
            await PromptAction("Checkout " + branch, "Name for the new local tracking branch", "Checkout", "", value => { vm.ContextBranchReference = branch; vm.RemoteLocalBranchName = value; }, vm.CheckoutContextRemoteCommand);
        else await vm.CheckoutBranchCommand.ExecuteAsync(null);
    }

    internal ContextMenu BuildBranchMenu(string branch)
    {
        if (DataContext is not MainWindowViewModel vm) return new ContextMenu();
        var remote = vm.Remotes.Any(r => branch.StartsWith(r.Name + "/", StringComparison.Ordinal));
        var items = new List<MenuItem>();
        void Add(string label, Func<Task> action, bool enabled = true)
        {
            var item = new MenuItem { Header = label, IsEnabled = enabled };
            item.Click += async (_, _) => { vm.SelectedBranchLine = branch; await action(); };
            items.Add(item);
        }
        Add("Checkout " + branch, () => CheckoutSidebarBranchAsync(branch), branch != vm.CurrentBranch && !branch.EndsWith("/HEAD", StringComparison.Ordinal));
        Add("Merge into " + vm.CurrentBranch + "…", () => PromptAction("Merge " + branch, "Merge into " + vm.CurrentBranch + ". Conflicts can be resolved in the merge workspace.", "Merge", "MERGE", _ => { }, vm.MergeSelectedBranchCommand), branch != vm.CurrentBranch);
        Add("Create branch here…", () => CreateBranchAtAsync(branch));
        Add("Create worktree here…", () => { vm.WorktreeBranch = branch; (TopLevel.GetTopLevel(this) as MainWindow)?.ShowDocument("Worktrees"); return Task.CompletedTask; }, !remote);
        if (!remote)
        {
            Add("Rename branch…", () => PromptAction("Rename " + branch, "New branch name", "Rename", "", value => vm.RenameBranchName = value, vm.RenameBranchCommand));
            Add("Delete merged branch…", () => PromptAction("Delete " + branch, "Only a merged, non-current branch can be deleted.", "Delete", branch, _ => { }, vm.DeleteBranchCommand), branch != vm.CurrentBranch);
            Add("Rebase current branch onto this…", () => { vm.RebaseTargetBranch = branch; (TopLevel.GetTopLevel(this) as MainWindow)?.ShowDocument("Branches"); return Task.CompletedTask; }, branch != vm.CurrentBranch);
        }
        Add("Show branch history", async () => { vm.SelectedHistoryScope = branch; await vm.ReloadHistoryCommand.ExecuteAsync(null); (TopLevel.GetTopLevel(this) as MainWindow)?.ShowDocument("History"); });
        Add("Copy branch name", async () => { if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(branch); });
        return new ContextMenu { ItemsSource = items };
    }
    private void BranchContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListBox list || DataContext is not MainWindowViewModel vm) return;
        var branch = (e.Source as Control)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()?.DataContext as string ?? list.SelectedItem as string;
        if (branch is null) return;
        vm.SelectedBranchLine = branch; list.ContextMenu = BuildBranchMenu(branch); list.ContextMenu.Open(list); e.Handled = true;
    }
    private async Task CreateBranchAtAsync(string reference)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        await PromptAction("Create branch", "New branch at " + reference, "Create", "", value => { vm.NewBranchName = value; vm.ContextBranchReference = reference; }, vm.CreateContextBranchCommand);
    }
    private async void CreateBranch_Click(object? sender, RoutedEventArgs e) => await CreateBranchAtAsync("HEAD");
    private async void CreateAnnotatedTag_Click(object? sender, RoutedEventArgs e)
    { if (DataContext is MainWindowViewModel vm) await TagCreationDialog.ShowAsync(this, vm, "HEAD"); }
    private async void CreateTag_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await PromptAction("Create tag at HEAD", "New lightweight tag name", "Create tag", "", value => vm.NewTagName = value, vm.CreateLightweightTagCommand);
    }
    private void TagContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not ListBox list || DataContext is not MainWindowViewModel vm) return;
        var tag = (e.Source as Control)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()?.DataContext as GitTagInfo ?? list.SelectedItem as GitTagInfo;
        if (tag is null) return;
        vm.SelectedTag = tag;
        var copy = new MenuItem { Header = "Copy tag name" };
        copy.Click += async (_, _) => { if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(tag.Name); };
        var push = new MenuItem { Header = "Push tag to selected remote", IsEnabled = vm.SelectedRemote is not null };
        push.Click += async (_, _) => { vm.SelectedTag = tag; await vm.PushSelectedTagCommand.ExecuteAsync(null); };
        var delete = new MenuItem { Header = "Delete local tag…" };
        delete.Click += async (_, _) => { vm.SelectedTag = tag; await PromptAction("Delete " + tag.Name, "Remote tags remain unchanged. Type the tag name to confirm.", "Delete", tag.Name, value => vm.TagDeletionConfirmation = value, vm.DeleteSelectedTagCommand); };
        var history = new MenuItem { Header = "Show tag history" };
        history.Click += (_, _) => { vm.SelectedHistoryScope = "refs/tags/" + tag.Name; (TopLevel.GetTopLevel(this) as MainWindow)?.ShowDocument("History"); };
        list.ContextMenu = new ContextMenu { ItemsSource = new[] { history, push, delete, copy } }; list.ContextMenu.Open(list); e.Handled = true;
    }
    private async Task PromptAction(string title, string description, string action, string expected, Action<string> setValue, IAsyncRelayCommand command)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner || DataContext is not MainWindowViewModel vm) return;
        var repository = vm.SelectedRepository?.Path; var branch = vm.SelectedBranchLine; var tag = vm.SelectedTag;
        var input = new TextBox { Watermark = expected.Length > 0 ? "Type " + expected : "Name" };
        var button = new Button { Content = action, HorizontalAlignment = HorizontalAlignment.Right }; button.Classes.Add("workspace");
        var dialog = new Window { Title = title, Width = 500, Height = 220, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var feedback = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 12, Children = { new TextBlock { Text = description, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, input, feedback, button } };
        button.Click += async (_, _) =>
        {
            if (vm.SelectedRepository?.Path != repository || vm.SelectedBranchLine != branch || vm.SelectedTag != tag) { feedback.Text = "Selection changed. Close and review the action again."; return; }
            if (string.IsNullOrWhiteSpace(input.Text) || (expected.Length > 0 && input.Text != expected)) { feedback.Text = expected.Length > 0 ? "Type " + expected + " to continue." : "Enter a name."; return; }
            setValue(input.Text!); dialog.Close(); await command.ExecuteAsync(null);
        };
        await dialog.ShowDialog(owner);
    }
}


