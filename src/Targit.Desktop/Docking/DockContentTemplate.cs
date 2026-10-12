using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Dock.Model.Mvvm.Controls;
using Targit.Desktop.ViewModels;

namespace Targit.Desktop.Docking;

/// <summary>Maps stable dockable IDs to actual repository-aware content.</summary>
public sealed class DockContentTemplate(MainWindowViewModel workspace) : IDataTemplate
{
    public bool Match(object? data) => data is Tool or Document;

    public Control Build(object? data)
    {
        var id = data switch
        {
            Tool tool => tool.Id,
            Document document => document.Id,
            _ => null
        };
        var root = new StackPanel { Margin = new Thickness(12), Spacing = 10, DataContext = workspace };
        var heading = new TextBlock { Text = id?.ToUpperInvariant(), FontWeight = FontWeight.SemiBold };
        root.Children.Add(heading);
        switch (id)
        {
            case "repositories":
                var repositories = new ListBox { MinHeight = 100 };
                repositories.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(workspace.Repositories)));
                repositories.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty, new Binding(nameof(workspace.SelectedRepository)) { Mode = BindingMode.TwoWay });
                repositories.ItemTemplate = new FuncDataTemplate<Targit.Core.RepositoryInfo>((repo, _) =>
                    new TextBlock { Text = repo is null ? "" : (repo.IsFavorite ? "★  " : "    ") + repo.Name });
                root.Children.Add(repositories);
                break;
            case "history":
                AddList(root, nameof(workspace.CommitLines), nameof(workspace.SelectedCommitLine));
                root.Children.Add(new TextBlock { Text = "Reset local HEAD to the selected ancestor. Soft preserves index and files; Mixed unstages changes; Hard restores tracked files. Requires clean working tree and unpublished HEAD.", TextWrapping = TextWrapping.Wrap });
                var resetMode = new ComboBox { ItemsSource = new[] { "Soft", "Mixed", "Hard" }, SelectedIndex = 0 };
                resetMode.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty, new Binding(nameof(workspace.ResetModeName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(resetMode);
                var resetConfirmation = new TextBox { Watermark = "Type RESET SOFT abc1234 (or MIXED/HARD)" };
                resetConfirmation.Bind(TextBox.TextProperty, new Binding(nameof(workspace.ResetConfirmation)) { Mode = BindingMode.TwoWay });
                root.Children.Add(resetConfirmation);
                var resetButton = new Button { Content = "Reset to selected ancestor (Hard is destructive)" };
                resetButton.Bind(Button.CommandProperty, new Binding(nameof(workspace.ResetToSelectedCommitCommand)));
                root.Children.Add(resetButton);
                break;
            case "changes":
                AddList(root, nameof(workspace.ChangeLines), nameof(workspace.SelectedChangeLine));
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var stage = new Button { Content = "Stage selected" };
                stage.Bind(Button.CommandProperty, new Binding(nameof(workspace.StageSelectedCommand)));
                var unstage = new Button { Content = "Unstage selected" };
                unstage.Bind(Button.CommandProperty, new Binding(nameof(workspace.UnstageSelectedCommand)));
                actions.Children.Add(stage);
                actions.Children.Add(unstage);
                root.Children.Add(actions);
                var stagedSummary = new TextBlock();
                stagedSummary.Bind(TextBlock.TextProperty, new Binding(nameof(workspace.StagingSummary)));
                root.Children.Add(stagedSummary);
                root.Children.Add(new TextBlock { Text = "Staged diff", FontWeight = FontWeight.SemiBold });
                var stagedPatch = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MinHeight = 110, FontFamily = FontFamily.Parse("Consolas, Menlo, monospace") };
                stagedPatch.Bind(TextBox.TextProperty, new Binding(nameof(workspace.StagedDiffText)));
                root.Children.Add(stagedPatch);
                root.Children.Add(new TextBlock { Text = "Unstaged diff", FontWeight = FontWeight.SemiBold });
                var unstagedPatch = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MinHeight = 110, FontFamily = FontFamily.Parse("Consolas, Menlo, monospace") };
                unstagedPatch.Bind(TextBox.TextProperty, new Binding(nameof(workspace.UnstagedDiffText)));
                root.Children.Add(unstagedPatch);
                root.Children.Add(new TextBlock { Text = "Commit staged changes", FontWeight = FontWeight.SemiBold });
                var message = new TextBox { Watermark = "Commit message", AcceptsReturn = true, MinHeight = 60 };
                message.Bind(TextBox.TextProperty, new Binding(nameof(workspace.CommitMessage)) { Mode = BindingMode.TwoWay });
                root.Children.Add(message);
                var name = new TextBox { Watermark = "Author name" };
                name.Bind(TextBox.TextProperty, new Binding(nameof(workspace.AuthorName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(name);
                var email = new TextBox { Watermark = "Author email" };
                email.Bind(TextBox.TextProperty, new Binding(nameof(workspace.AuthorEmail)) { Mode = BindingMode.TwoWay });
                root.Children.Add(email);
                var commit = new Button { Content = "Commit staged" };
                commit.Bind(Button.CommandProperty, new Binding(nameof(workspace.CommitCommand)));
                root.Children.Add(commit);
                break;
            case "branches":
                AddList(root, nameof(workspace.BranchLines), nameof(workspace.SelectedBranchLine));
                var branchName = new TextBox { Watermark = "New local branch name" };
                branchName.Bind(TextBox.TextProperty, new Binding(nameof(workspace.NewBranchName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(branchName);
                var createBranch = new Button { Content = "Create branch" };
                createBranch.Bind(Button.CommandProperty, new Binding(nameof(workspace.CreateBranchCommand)));
                root.Children.Add(createBranch);
                var checkoutBranch = new Button { Content = "Switch to selected branch" };
                checkoutBranch.Bind(Button.CommandProperty, new Binding(nameof(workspace.CheckoutBranchCommand)));
                root.Children.Add(checkoutBranch);
                var deleteBranch = new Button { Content = "Delete selected local branch" };
                deleteBranch.Bind(Button.CommandProperty, new Binding(nameof(workspace.DeleteBranchCommand)));
                root.Children.Add(deleteBranch);
                var renameBranchName = new TextBox { Watermark = "New name for selected local branch" };
                renameBranchName.Bind(TextBox.TextProperty, new Binding(nameof(workspace.RenameBranchName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(renameBranchName);
                var renameBranch = new Button { Content = "Rename selected local branch" };
                renameBranch.Bind(Button.CommandProperty, new Binding(nameof(workspace.RenameBranchCommand)));
                root.Children.Add(renameBranch);
                break;
            case "remotes":
                var remoteList = new ListBox { MinHeight = 150 };
                remoteList.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(workspace.Remotes)));
                remoteList.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty,
                    new Binding(nameof(workspace.SelectedRemote)) { Mode = BindingMode.TwoWay });
                remoteList.ItemTemplate = new FuncDataTemplate<Targit.Git.GitRemoteInfo>((remote, _) =>
                    new TextBlock { Text = remote is null ? "" : remote.Name + " — " + remote.Url, TextWrapping = TextWrapping.Wrap });
                root.Children.Add(remoteList);
                var remoteName = new TextBox { Watermark = "Remote name (e.g. origin)" };
                remoteName.Bind(TextBox.TextProperty, new Binding(nameof(workspace.NewRemoteName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(remoteName);
                var remoteUrl = new TextBox { Watermark = "Remote URL" };
                remoteUrl.Bind(TextBox.TextProperty, new Binding(nameof(workspace.NewRemoteUrl)) { Mode = BindingMode.TwoWay });
                root.Children.Add(remoteUrl);
                var addRemote = new Button { Content = "Add remote" };
                addRemote.Bind(Button.CommandProperty, new Binding(nameof(workspace.AddRemoteCommand)));
                root.Children.Add(addRemote);
                var removeRemote = new Button { Content = "Remove selected remote" };
                removeRemote.Bind(Button.CommandProperty, new Binding(nameof(workspace.RemoveRemoteCommand)));
                root.Children.Add(removeRemote);
                var fetchRemote = new Button { Content = "Fetch selected remote" };
                fetchRemote.Bind(Button.CommandProperty, new Binding(nameof(workspace.FetchRemoteCommand)));
                root.Children.Add(fetchRemote);
                var pushRemote = new Button { Content = "Push current branch (no force)" };
                pushRemote.Bind(Button.CommandProperty, new Binding(nameof(workspace.PushRemoteCommand)));
                root.Children.Add(pushRemote);
                var pullRemote = new Button { Content = "Pull tracked branch (fast-forward only)" };
                pullRemote.Bind(Button.CommandProperty, new Binding(nameof(workspace.PullFastForwardCommand)));
                root.Children.Add(pullRemote);
                root.Children.Add(new TextBlock { Text = "Fetched branches (select to track)", FontWeight = FontWeight.SemiBold });
                var remoteBranches = new ListBox { MinHeight = 110 };
                remoteBranches.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(workspace.RemoteBranches)));
                remoteBranches.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty,
                    new Binding(nameof(workspace.SelectedRemoteBranch)) { Mode = BindingMode.TwoWay });
                remoteBranches.ItemTemplate = new FuncDataTemplate<Targit.Git.GitRemoteBranchInfo>((branch, _) =>
                    new TextBlock { Text = branch is null ? "" : (branch.IsTrackedByCurrentBranch ? "✓ " : "") + branch.BranchName + "  " + branch.CommitSha[..7] });
                root.Children.Add(remoteBranches);
                var checkoutRemoteBranch = new Button { Content = "Checkout selected remote branch as local" };
                checkoutRemoteBranch.Bind(Button.CommandProperty, new Binding(nameof(workspace.CheckoutRemoteBranchCommand)));
                root.Children.Add(checkoutRemoteBranch);
                var upstreamBranch = new TextBox { Watermark = "Remote branch to track (e.g. main)" };
                upstreamBranch.Bind(TextBox.TextProperty, new Binding(nameof(workspace.UpstreamBranchName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(upstreamBranch);
                var setUpstream = new Button { Content = "Track selected remote branch" };
                setUpstream.Bind(Button.CommandProperty, new Binding(nameof(workspace.SetUpstreamCommand)));
                root.Children.Add(setUpstream);
                var clearUpstream = new Button { Content = "Clear current branch upstream" };
                clearUpstream.Bind(Button.CommandProperty, new Binding(nameof(workspace.ClearUpstreamCommand)));
                root.Children.Add(clearUpstream);
                break;
            case "inspector":
                var current = new TextBlock();
                current.TextWrapping = TextWrapping.Wrap;
                current.Bind(TextBlock.TextProperty, new Binding(nameof(workspace.InspectorText)));
                root.Children.Add(current);
                break;
            case "output":
                var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
                status.Bind(TextBlock.TextProperty, new Binding(nameof(workspace.Status)));
                root.Children.Add(status);
                break;
            default:
                root.Children.Add(new TextBlock { Text = "No content available." });
                break;
        }
        return new ScrollViewer { Content = root };
    }

    private static void AddList(StackPanel root, string property, string selectionProperty)
    {
        var list = new ListBox { MinHeight = 150 };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding(property));
        list.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty, new Binding(selectionProperty) { Mode = BindingMode.TwoWay });
        root.Children.Add(list);
    }
}