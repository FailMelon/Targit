using LibGit2Sharp;
namespace GitLlama.Git;
public sealed record GitCommitSearchHit(string Sha, string Subject, string Author);
public sealed class GitCommitSearchService
{
    public Task<IReadOnlyList<GitCommitSearchHit>> SearchAsync(string repositoryPath, string query, int maxResults = 100, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitCommitSearchHit>>(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (maxResults is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maxResults));
        using var repo = new Repository(repositoryPath);
        var hits = new List<GitCommitSearchHit>();
        foreach (var commit in repo.Commits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!commit.Message.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !commit.Author.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !commit.Sha.StartsWith(query, StringComparison.OrdinalIgnoreCase)) continue;
            hits.Add(new GitCommitSearchHit(commit.Sha, commit.MessageShort, commit.Author.Name));
            if (hits.Count == maxResults) break;
        }
        return hits;
    }, cancellationToken);
}