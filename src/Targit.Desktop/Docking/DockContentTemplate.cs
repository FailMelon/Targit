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
                root.Children.Add(new TextBlock { Text = "Lightweight tags (current HEAD)", FontWeight = FontWeight.SemiBold });
                var tagName = new TextBox { Watermark = "Tag name (e.g. v1.0.0)" };
                tagName.Bind(TextBox.TextProperty, new Binding(nameof(workspace.NewTagName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(tagName);
                var createTag = new Button { Content = "Create lightweight tag" };
                createTag.Bind(Button.CommandProperty, new Binding(nameof(workspace.CreateLightweightTagCommand)));
                root.Children.Add(createTag);
                root.Children.Add(new TextBlock { Text = "Annotated tag (current HEAD)", FontWeight = FontWeight.SemiBold });
                var annotatedName = new TextBox { Watermark = "Annotated tag name (e.g. v2.0.0)" };
                annotatedName.Bind(TextBox.TextProperty, new Binding(nameof(workspace.AnnotatedTagName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(annotatedName);
                var annotatedMessage = new TextBox { Watermark = "Tag annotation message", AcceptsReturn = true, MinHeight = 55 };
                annotatedMessage.Bind(TextBox.TextProperty, new Binding(nameof(workspace.AnnotatedTagMessage)) { Mode = BindingMode.TwoWay });
                root.Children.Add(annotatedMessage);
                var annotatedAuthor = new TextBox { Watermark = "Tagger name" };
                annotatedAuthor.Bind(TextBox.TextProperty, new Binding(nameof(workspace.AnnotatedTagAuthorName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(annotatedAuthor);
                var annotatedEmail = new TextBox { Watermark = "Tagger email" };
                annotatedEmail.Bind(TextBox.TextProperty, new Binding(nameof(workspace.AnnotatedTagAuthorEmail)) { Mode = BindingMode.TwoWay });
                root.Children.Add(annotatedEmail);
                var createAnnotatedTag = new Button { Content = "Create annotated tag" };
                createAnnotatedTag.Bind(Button.CommandProperty, new Binding(nameof(workspace.CreateAnnotatedTagCommand)));
                root.Children.Add(createAnnotatedTag);
                var tagList = new ListBox { MinHeight = 80 };
                tagList.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(workspace.Tags)));
                tagList.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty, new Binding(nameof(workspace.SelectedTag)) { Mode = BindingMode.TwoWay });
                tagList.ItemTemplate = new FuncDataTemplate<Targit.Git.GitTagInfo>((tag, _) =>
                    new TextBlock { Text = tag is null ? "" : tag.Name + "  " + tag.TargetSha[..7] + (tag.IsAnnotated ? " (annotated)" : "") });
                root.Children.Add(tagList);
                root.Children.Add(new TextBlock { Text = "Remote tags: select a local tag above and a remote in the Remotes panel.", TextWrapping = TextWrapping.Wrap });
                var pushTag = new Button { Content = "Push selected tag to selected remote (no force)" };
                pushTag.Bind(Button.CommandProperty, new Binding(nameof(workspace.PushSelectedTagCommand)));
                root.Children.Add(pushTag);
                root.Children.Add(new TextBlock { Text = "Delete remote tag only: type remote/tag exactly to confirm. Local tag is preserved.", TextWrapping = TextWrapping.Wrap });
                var confirmRemoteTag = new TextBox { Watermark = "remote/tag (e.g. origin/v1.0.0)" };
                confirmRemoteTag.Bind(TextBox.TextProperty, new Binding(nameof(workspace.RemoteTagDeletionConfirmation)) { Mode = BindingMode.TwoWay });
                root.Children.Add(confirmRemoteTag);
                var deleteRemoteTag = new Button { Content = "Delete selected tag from remote" };
                deleteRemoteTag.Bind(Button.CommandProperty, new Binding(nameof(workspace.DeleteSelectedRemoteTagCommand)));
                root.Children.Add(deleteRemoteTag);
                root.Children.Add(new TextBlock { Text = "Delete local tag: select a tag and type its exact name to confirm. Remote tags are unaffected.", TextWrapping = TextWrapping.Wrap });
                var confirmTagDeletion = new TextBox { Watermark = "Exact tag name to confirm deletion" };
                confirmTagDeletion.Bind(TextBox.TextProperty, new Binding(nameof(workspace.TagDeletionConfirmation)) { Mode = BindingMode.TwoWay });
                root.Children.Add(confirmTagDeletion);
                var deleteTag = new Button { Content = "Delete selected local tag" };
                deleteTag.Bind(Button.CommandProperty, new Binding(nameof(workspace.DeleteSelectedTagCommand)));
                root.Children.Add(deleteTag);
                break;
            case "changes":
                AddList(root, nameof(workspace.ChangeLines), nameof(workspace.SelectedChangeLine));
                root.Children.Add(new TextBlock { Text = "Stash tracked changes (untracked files are excluded)", FontWeight = FontWeight.SemiBold });
                var stashMessage = new TextBox { Watermark = "Optional stash message" };
                stashMessage.Bind(TextBox.TextProperty, new Binding(nameof(workspace.StashMessage)) { Mode = BindingMode.TwoWay });
                root.Children.Add(stashMessage);
                var saveStash = new Button { Content = "Stash tracked changes" };
                saveStash.Bind(Button.CommandProperty, new Binding(nameof(workspace.SaveTrackedStashCommand)));
                root.Children.Add(saveStash);
                var stashList = new ListBox { MinHeight = 70 };
                stashList.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(workspace.Stashes)));
                stashList.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty, new Binding(nameof(workspace.SelectedStash)) { Mode = BindingMode.TwoWay });
                stashList.ItemTemplate = new FuncDataTemplate<Targit.Git.GitStashInfo>((stash, _) =>
                    new TextBlock { Text = stash is null ? "" : $"stash@{{{stash.Index}}}: {stash.Message}" });
                root.Children.Add(stashList);
                root.Children.Add(new TextBlock { Text = "Apply selected stash to a clean working tree (stash remains saved).", TextWrapping = TextWrapping.Wrap });
                var applyStash = new Button { Content = "Apply selected stash (keep entry)" };
                applyStash.Bind(Button.CommandProperty, new Binding(nameof(workspace.ApplySelectedStashCommand)));
                root.Children.Add(applyStash);
                root.Children.Add(new TextBlock { Text = "Pop applies the selected stash to a clean working tree and removes its entry only if application succeeds.", TextWrapping = TextWrapping.Wrap });
                var popStash = new Button { Content = "Pop selected stash (apply and remove)" };
                popStash.Bind(Button.CommandProperty, new Binding(nameof(workspace.PopSelectedStashCommand)));
                root.Children.Add(popStash);
                root.Children.Add(new TextBlock { Text = "Permanently drop a stash without applying it. Select an entry and type stash@{index} to confirm.", TextWrapping = TextWrapping.Wrap });
                var confirmStashDrop = new TextBox { Watermark = "Type stash@{0} for selected stash" };
                confirmStashDrop.Bind(TextBox.TextProperty, new Binding(nameof(workspace.StashDropConfirmation)) { Mode = BindingMode.TwoWay });
                root.Children.Add(confirmStashDrop);
                var dropStash = new Button { Content = "Permanently drop selected stash" };
                dropStash.Bind(Button.CommandProperty, new Binding(nameof(workspace.DropSelectedStashCommand)));
                root.Children.Add(dropStash);
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
                root.Children.Add(new TextBlock { Text = "Effective Git identity", FontWeight = FontWeight.SemiBold });
                var identity = new TextBlock { TextWrapping = TextWrapping.Wrap };
                identity.Bind(TextBlock.TextProperty, new Binding(nameof(workspace.ConfiguredIdentity)));
                root.Children.Add(identity);
                root.Children.Add(new TextBlock { Text = "Repository-local identity (does not modify global Git settings)" });
                var localName = new TextBox { Watermark = "Git author name" };
                localName.Bind(TextBox.TextProperty, new Binding(nameof(workspace.LocalIdentityName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(localName);
                var localEmail = new TextBox { Watermark = "Git author email" };
                localEmail.Bind(TextBox.TextProperty, new Binding(nameof(workspace.LocalIdentityEmail)) { Mode = BindingMode.TwoWay });
                root.Children.Add(localEmail);
                var saveLocalIdentity = new Button { Content = "Save local Git identity" };
                saveLocalIdentity.Bind(Button.CommandProperty, new Binding(nameof(workspace.SaveLocalIdentityCommand)));
                root.Children.Add(saveLocalIdentity);
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