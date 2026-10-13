using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Conservative linear rebase: replay commits onto a different local branch.</summary>
public sealed class GitLinearRebaseService
{
    public Task<string> RebaseOntoAsync(string repositoryPath, string targetBranch,
        CancellationToken cancellationToken = default) => RebaseWithPlanAsync(repositoryPath, targetBranch, null, cancellationToken);

    public Task<string> RebaseWithPlanAsync(string repositoryPath, string targetBranch,
        IReadOnlyList<GitRebaseStep>? plan, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetBranch);
        cancellationToken.ThrowIfCancellationRequested();
        using var repo = new Repository(repositoryPath);
        if (repo.Info.IsBare || repo.Info.IsHeadDetached || repo.Head.Tip is null)
            throw new InvalidOperationException("Rebase requires a checked-out local branch.");
        if (repo.Info.CurrentOperation != CurrentOperation.None || repo.RetrieveStatus().IsDirty)
            throw new InvalidOperationException("Finish current operations and commit or stash changes before rebasing.");
        var target = repo.Branches[targetBranch]
            ?? throw new ArgumentException("Target branch not found.", nameof(targetBranch));
        if (target.IsRemote || target.Tip is null)
            throw new InvalidOperationException("Choose a local target branch.");
        if (target.FriendlyName == repo.Head.FriendlyName)
            throw new InvalidOperationException("Cannot rebase a branch onto itself.");
        var source = repo.Head.Tip;
        var baseCommit = repo.ObjectDatabase.FindMergeBase(source, target.Tip)
            ?? throw new InvalidOperationException("Branches have no common ancestor.");
        var commits = repo.Commits.QueryBy(new CommitFilter
        {
            IncludeReachableFrom = source,
            ExcludeReachableFrom = baseCommit,
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse
        }).ToArray();
        if (commits.Any(c => c.Parents.Count() != 1))
            throw new InvalidOperationException("This rebase supports only linear histories.");
        if (commits.Length == 0)
            throw new InvalidOperationException("There are no commits to replay.");
        if (plan is not null)
        {
            GitRebasePlanService.ValidatePlan(plan);
            if (plan.Count != commits.Length || !plan.Select(x => x.CommitSha).OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(commits.Select(x => x.Sha).OrderBy(x => x, StringComparer.Ordinal)))
                throw new InvalidOperationException("The rebase plan must contain each outgoing commit exactly once.");
            if (plan.Any(x => x.Action is GitRebaseAction.Edit or GitRebaseAction.Reword))
                throw new NotSupportedException("Edit and Reword require a separate message-editing workflow.");
            commits = plan.Where(x => x.Action != GitRebaseAction.Drop)
                .Select(x => commits.Single(c => c.Sha == x.CommitSha)).ToArray();
        }
        // Create a temporary replay branch so failure does not rewrite the source branch.
        var tempName = "targit-rebase-" + Guid.NewGuid().ToString("N");
        var originalBranch = repo.Head.FriendlyName;
        var temporary = repo.CreateBranch(tempName, target.Tip);
        var completed = false;
        try
        {
            Commands.Checkout(repo, temporary);
            foreach (var commit in commits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var action = plan?.SingleOrDefault(x => x.CommitSha == commit.Sha)?.Action ?? GitRebaseAction.Pick;
                var result = repo.CherryPick(commit, commit.Committer,
                    new CherryPickOptions { CommitOnSuccess = true });
                if (result.Status != CherryPickStatus.CherryPicked)
                    throw new InvalidOperationException($"Rebase stopped at {commit.Sha[..7]}: {result.Status}. Original branch is unchanged.");
                if (action is GitRebaseAction.Squash or GitRebaseAction.Fixup)
                {
                    var tip = repo.Head.Tip!;
                    var previous = tip.Parents.Single();
                    var combinedMessage = action == GitRebaseAction.Squash
                        ? previous.Message.TrimEnd() + "\n\n" + commit.Message.TrimEnd()
                        : previous.Message;
                    var merged = repo.ObjectDatabase.CreateCommit(previous.Author, previous.Committer,
                        combinedMessage, tip.Tree, previous.Parents, false);
                    repo.Refs.UpdateTarget(repo.Head.Reference.ResolveToDirectReference(), merged.Id);
                    repo.Reset(ResetMode.Hard, merged);
                }
            }
            var rewritten = repo.Head.Tip
                ?? throw new InvalidOperationException("Rebase produced no commit.");
            // Move the original branch only after successful replay, then check out the
            // rewritten branch to synchronize HEAD, index and working tree.
            repo.Refs.UpdateTarget(repo.Branches[originalBranch].Reference, rewritten.Id);
            Commands.Checkout(repo, repo.Branches[originalBranch], new CheckoutOptions
            {
                CheckoutModifiers = CheckoutModifiers.Force
            });
            completed = true;
            return rewritten.Sha;
        }
        finally
        {
            if (!completed && repo.Info.CurrentOperation != CurrentOperation.None)
                repo.Reset(ResetMode.Hard, repo.Head.Tip);
            if (repo.Head.FriendlyName == tempName)
                Commands.Checkout(repo, repo.Branches[originalBranch], new CheckoutOptions
                {
                    CheckoutModifiers = CheckoutModifiers.Force
                });
            if (repo.Head.FriendlyName != tempName)
                repo.Branches.Remove(tempName);
        }
    }, cancellationToken);
}
