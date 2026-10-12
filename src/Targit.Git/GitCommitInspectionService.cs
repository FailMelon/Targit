using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitCommitSummary(string Sha, string Message, string Author, DateTimeOffset When);

/// <summary>Reads recent commits reachable from the current HEAD.</summary>
public sealed class GitCommitInspectionService
{
    public Task<IReadOnlyList<GitCommitSummary>> ListRecentAsync(string repositoryPath, int limit = 50,
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitCommitSummary>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        using var repository = new Repository(repositoryPath);
        if (repository.Head.Tip is null) return Array.Empty<GitCommitSummary>();
        return repository.Commits.QueryBy(new CommitFilter
        {
            IncludeReachableFrom = repository.Head,
            SortBy = CommitSortStrategies.Time
        }).Take(limit).Select(commit =>
            new GitCommitSummary(commit.Sha, commit.MessageShort, commit.Author.Name,
                commit.Author.When)).ToArray();
    }, cancellationToken);
}
