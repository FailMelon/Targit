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
        var upstream = repo.Lookup<Commit>(upstreamSha)
            ?? throw new ArgumentException("Upstream commit not found.", nameof(upstreamSha));
        if (repo.Head.Tip is null)
            throw new InvalidOperationException("Repository has no HEAD commit.");
        var filter = new CommitFilter { IncludeReachableFrom = repo.Head.Tip, ExcludeReachableFrom = upstream,
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse };
        return repo.Commits.QueryBy(filter)
            .Select(commit => new GitRebaseStep(commit.Sha, commit.MessageShort, GitRebaseAction.Pick))
            .ToArray();
    }, cancellationToken);

    public static void ValidatePlan(IReadOnlyList<GitRebaseStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Any(step => string.IsNullOrWhiteSpace(step.CommitSha)))
            throw new ArgumentException("Every rebase step must reference a commit.", nameof(steps));
        if (steps.Select(step => step.CommitSha).Distinct(StringComparer.Ordinal).Count() != steps.Count)
            throw new ArgumentException("A commit may appear only once.", nameof(steps));
    }
}
