using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using GitLlama.Desktop.ViewModels;

namespace GitLlama.Desktop.Views;

public partial class RemoteTreeView : UserControl
{
    public RemoteTreeView() => AvaloniaXamlLoader.Load(this);
    private RemoteTreeNode? EventNode(object? source) => (source as Control)?.GetSelfAndVisualAncestors().OfType<TreeViewItem>().FirstOrDefault()?.DataContext as RemoteTreeNode;
    private void SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && e.AddedItems.OfType<RemoteTreeNode>().FirstOrDefault() is { } node)
        {
            if (node.Remote is not null) vm.SelectedRemote = node.Remote;
            if (node.Branch is { } branch)
            {
                vm.SelectedRemote = vm.Remotes.FirstOrDefault(r => branch.StartsWith(r.Name + "/", StringComparison.Ordinal));
                vm.SelectedBranchLine = branch;
                if (vm.SelectedRemote is { } remote) vm.UpstreamBranchName = branch[(remote.Name.Length + 1)..];
            }
        }
    }
    private async void BranchTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && EventNode(e.Source)?.Branch is { } branch)
        { e.Handled = true; await vm.RevealBranchHistoryCommand.ExecuteAsync(branch); }
    }
    private async void BranchDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (EventNode(e.Source)?.Branch is { } branch) { e.Handled = true; await CheckoutAsync(branch); }
    }
    private async Task CheckoutAsync(string branch)
    {
        if (DataContext is not MainWindowViewModel vm || branch.EndsWith("/HEAD", StringComparison.Ordinal)) return;
        await PromptAsync("Checkout " + branch, "Name for the new local tracking branch", "Checkout", "", value => { vm.ContextBranchReference = branch; vm.RemoteLocalBranchName = value; }, () => vm.CheckoutContextRemoteCommand.ExecuteAsync(null));
    }
    private void TreeContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        var node = EventNode(e.Source) ?? this.FindControl<TreeView>("RemoteTreeControl")?.SelectedItem as RemoteTreeNode;
        if (node is null || (node.Remote is null && node.Branch is null)) return;
        var control = (e.Source as Control)?.GetSelfAndVisualAncestors().OfType<TreeViewItem>().FirstOrDefault() as Control ?? this;
        control.ContextMenu = BuildMenu(node); control.ContextMenu.Open(control); e.Handled = true;
    }
    internal ContextMenu BuildMenu(RemoteTreeNode node)
    {
        if (DataContext is not MainWindowViewModel vm) return new ContextMenu();
        if (node.Branch is { } branch && this.GetVisualAncestors().OfType<NavigationView>().FirstOrDefault() is { } navigation) return navigation.BuildBranchMenu(branch);
        var items = new List<MenuItem>();
        void Add(string label, Func<Task> action) { var item = new MenuItem { Header = label }; item.Click += async (_, _) => await action(); items.Add(item); }
        if (node.Remote is { } remote)
        {
            Add("Fetch " + remote.Name, () => { vm.SelectedRemote = remote; return vm.FetchRemoteCommand.ExecuteAsync(null); });
            Add("Push " + vm.CurrentBranch + " to " + remote.Name, () => { vm.SelectedRemote = remote; return vm.PushRemoteCommand.ExecuteAsync(null); });
            Add("Remote settings…", () => { vm.SelectedRemote = remote; vm.DockDocument?.Invoke("Remotes"); return Task.CompletedTask; });
            Add("Remove " + remote.Name + "…", () => PromptAsync("Remove " + remote.Name, "Remove this remote configuration and its remote-tracking references. Type its name to confirm.", "Remove", remote.Name, _ => vm.SelectedRemote = remote, () => vm.RemoveRemoteCommand.ExecuteAsync(null)));
            Add("Copy remote URL", () => CopyAsync(remote.Url));
        }
        else if (node.Branch is { } reference)
        {
            Add("Checkout " + reference + "…", () => CheckoutAsync(reference));
            Add("Show branch history", () => vm.RevealBranchHistoryCommand.ExecuteAsync(reference));
            Add("Copy branch name", () => CopyAsync(reference));
        }
        return new ContextMenu { ItemsSource = items };
    }
    private async Task CopyAsync(string text) { if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(text); }
    private async Task PromptAsync(string title, string description, string action, string expected, Action<string> setValue, Func<Task> execute)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner || DataContext is not MainWindowViewModel vm) return;
        var path = vm.SelectedRepository?.Path;
        var input = new TextBox { Watermark = expected.Length == 0 ? "Local branch name" : "Type " + expected };
        var feedback = new TextBlock(); var button = new Button { Content = action, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var dialog = new Window { Title = title, Width = 520, Height = 240, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel { Margin = new Thickness(18), Spacing = 12, Children = { new TextBlock { Text = description, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, input, feedback, button } } };
        button.Click += async (_, _) =>
        {
            if (path != vm.SelectedRepository?.Path) { feedback.Text = "Repository changed. Close and review the action again."; return; }
            if (string.IsNullOrWhiteSpace(input.Text) || (expected.Length > 0 && input.Text != expected)) { feedback.Text = "Enter " + (expected.Length > 0 ? expected : "a local branch name") + " to continue."; return; }
            setValue(input.Text!); dialog.Close(); await execute();
        };
        await dialog.ShowDialog(owner);
    }
}
