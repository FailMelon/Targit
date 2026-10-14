using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using GitLlama.Git;

namespace GitLlama.Desktop.ViewModels;

public sealed partial class StashTreeNode : ObservableObject
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public GitStashInfo? Stash { get; init; }
    public string Reference => Stash is null ? "" : $"stash@{{{Stash.Index}}}";
    public string IconPath =>
        Stash is null
            ? "M 5,4 L 5,14 M 5,10 C 5,6 14,10 14,4 M 5,1 A 2,2 0 1 0 5,5 A 2,2 0 1 0 5,1 M 14,1 A 2,2 0 1 0 14,5 A 2,2 0 1 0 14,1 M 5,13 A 2,2 0 1 0 5,17 A 2,2 0 1 0 5,13"
            : "M 2,6 L 16,6 L 16,15 L 2,15 Z M 1,3 L 17,3 L 17,6 L 1,6 Z M 7,9 L 11,9";
    public string Tooltip => Stash is null ? "Stashes saved on " + Name : Reference + ": " + Stash.Message;
    public ObservableCollection<StashTreeNode> Children { get; } = [];

    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    private string count = "";
}

public partial class MainWindowViewModel
{
    public ObservableCollection<StashTreeNode> StashTree { get; } = [];

    [ObservableProperty]
    private bool hasStashes;

    /// <summary>Whether the working changes inspector stashes changes instead of committing them.</summary>
    [ObservableProperty]
    private bool isStashMode;

    /// <summary>Why the working changes can't be stashed right now, or null when they can.</summary>
    public string? StashBlockedReason =>
        !HasRepository || WorkingFiles.Count == 0 ? "There are no changes to stash."
        : WorkingFiles.Any(file => file.IsStaged) ? null
        : "Only staged changes are stashed. Stage the changes you want to stash.";

    public bool CanStash => StashBlockedReason is null;

    private void NotifyStashAvailability()
    {
        OnPropertyChanged(nameof(StashBlockedReason));
        OnPropertyChanged(nameof(CanStash));
        SaveStagedStashCommand.NotifyCanExecuteChanged();
    }

    private string? _inspectedStashSha;

    /// <summary>Shows a stash's changes in the inspector, the same way a commit is inspected.</summary>
    public void InspectStash(GitStashInfo stash)
    {
        if (SelectedRepository is null || stash.Sha.Length == 0)
        {
            return;
        }

        _inspectedStashSha = stash.Sha;
        IsWorkingTreeInspector = false;
        IsFileReview = false;
        using (BeginContextSnapshot())
        {
            // Deselect the history row so selecting it again inspects that commit.
            SelectedHistoryRow = null;
        }

        _ = LoadHistoryCommitAsync(stash.Sha);
    }

    partial void OnIsWorkingTreeInspectorChanged(bool value)
    {
        if (value)
        {
            _inspectedStashSha = null;
        }
    }

    private static readonly Regex StashMessagePattern = new(@"^(?:WIP on|On) ([^:]+): (.*)$", RegexOptions.Singleline);

    /// <summary>Splits a stash message such as "On main: message" into its branch and description.</summary>
    public static (string Branch, string Description) DescribeStash(string message)
    {
        var match = StashMessagePattern.Match(message);
        if (!match.Success)
        {
            return ("Other", message);
        }

        var description = match.Groups[2].Value.Trim();
        if (message.StartsWith("WIP on ", StringComparison.Ordinal))
        {
            // "WIP on main: 1a2b3c4 subject" carries the HEAD commit instead of a user message.
            var space = description.IndexOf(' ');
            description = "WIP: " + (space > 0 ? description[(space + 1)..] : description);
        }

        return (match.Groups[1].Value, description.Length == 0 ? "(no message)" : description);
    }

    private void UpdateStashTree()
    {
        var roots = new List<StashTreeNode>();
        foreach (var group in Stashes.GroupBy(stash => DescribeStash(stash.Message).Branch))
        {
            var root =
                StashTree.FirstOrDefault(n => n.Key == group.Key)
                ?? new StashTreeNode { Key = group.Key, Name = group.Key };
            root.Count = group.Count().ToString();
            CollectionUpdates.Sync(
                root.Children,
                group.Select(stash => new StashTreeNode
                {
                    Key = $"{stash.Index}:{stash.Message}",
                    Name = DescribeStash(stash.Message).Description,
                    Stash = stash,
                }),
                n => n.Key
            );
            roots.Add(root);
        }

        // Keep the current branch first, then the most recent stash order.
        CollectionUpdates.Sync(StashTree, roots.OrderBy(n => n.Key != CurrentBranch), node => node.Key);
        HasStashes = Stashes.Count > 0;
    }
}
