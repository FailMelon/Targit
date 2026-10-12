using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitBranchDivergence(string BranchName, string UpstreamName, int Ahead, int Behind);

/// <summary>Reports divergence from the locally fetched upstream without network requests.</summary>
public sealed class GitBranchDivergenceService
{
    public Task<GitBranchDivergence> GetCurrentAsync(string repositoryPath,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repository = new Repository(repositoryPath);
        var head = repository.Head;
        if (head.Tip is null || !head.IsTracking || head.TrackedBranch?.Tip is null)
            throw new InvalidOperationException("A checked-out branch with a fetched upstream is required.");
        var upstream = head.TrackedBranch;
        var divergence = repository.ObjectDatabase.CalculateHistoryDivergence(head.Tip, upstream.Tip);
        cancellationToken.ThrowIfCancellationRequested();
        return new GitBranchDivergence(head.FriendlyName, upstream.FriendlyName,
            divergence.AheadBy ?? 0, divergence.BehindBy ?? 0);
    }, cancellationToken);
}
