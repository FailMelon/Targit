using LibGit2Sharp;

namespace Targit.Git;

public enum GitRebaseAction { Pick, Reword, Edit, Squash, Fixup, Drop }
public sealed record GitRebaseStep(string CommitSha, string Subject, GitRebaseAction Action);

/// <summary>Phase 34 Git backend service.</summary>
public sealed class GitRebasePlanService
{
    public Task<IReadOnlyList<GitRebaseStep>> PlanAsync(string repositoryPath, string upstreamSha, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitRebaseStep>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(upstreamSha);
        using var repo = new Repository(repositoryPath);
        if (repo.Info.IsBare || repo.Info.IsHeadDetached)\n            throw new InvalidOperationException("Rebase planning requires a checked-out local branch.");\n        if (repo.Info.CurrentOperation != CurrentOperation.None)\n            throw new InvalidOperationException("Complete the current Git operation before planning a rebase.");\n        var upstream = repo.Lookup<Commit>(upstreamSha)
            ?? throw new ArgumentException("Upstream commit not found.", nameof(upstreamSha));
        if (repo.Head.Tip is null)
            throw new InvalidOperationException("Repository has no HEAD commit.");
        if (!repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = repo.Head.Tip })\n            .Any(commit => commit.Id == upstream.Id))\n            throw new InvalidOperationException("Upstream must be an ancestor of HEAD.");\n        var filter = new CommitFilter { IncludeReachableFrom = repo.Head.Tip, ExcludeReachableFrom = upstream,
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse };
        var commits = repo.Commits.QueryBy(filter).ToArray();\n        if (commits.Any(commit => commit.Parents.Skip(1).Any()))\n            throw new InvalidOperationException("Merge commits are not supported by the linear rebase planner.");\n        return commits
            .Select(commit => new GitRebaseStep(commit.Sha, commit.MessageShort, GitRebaseAction.Pick))
            .ToArray();
    }, cancellationToken);

    public static void ValidatePlan(IReadOnlyList<GitRebaseStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Any(step => step is null || string.IsNullOrWhiteSpace(step.CommitSha)))
            throw new ArgumentException("Every rebase step must reference a commit.", nameof(steps));
        if (steps.Any(step => !Enum.IsDefined(step.Action)))\n            throw new ArgumentException("Unknown rebase action.", nameof(steps));\n        if (steps.Count > 0 && steps[0].Action is GitRebaseAction.Squash or GitRebaseAction.Fixup)\n            throw new ArgumentException("The first step cannot squash or fixup without a previous commit.", nameof(steps));\n        if (steps.Select(step => step.CommitSha).Distinct(StringComparer.Ordinal).Count() != steps.Count)
            throw new ArgumentException("A commit may appear only once.", nameof(steps));\n        var hasPreviousCommit = false;\n        foreach (var step in steps)\n        {\n            if (step.Action is GitRebaseAction.Squash or GitRebaseAction.Fixup && !hasPreviousCommit)\n                throw new ArgumentException("Squash and fixup require a preceding retained commit.", nameof(steps));\n            if (step.Action != GitRebaseAction.Drop) hasPreviousCommit = true;\n        }
    }
}
