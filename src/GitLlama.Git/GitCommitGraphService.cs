using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitCommitNode(string Sha, string Subject, string Author, DateTimeOffset Timestamp, IReadOnlyList<string> Parents, IReadOnlyList<string> Labels, string AuthorEmail = "");
public sealed record GitHistoryPage(IReadOnlyList<GitCommitNode> Commits, bool HasMore);

/// <summary>Phase 36 Git backend service.</summary>
public sealed class GitCommitGraphService
{
    public Task<GitHistoryPage> GetPageAsync(string repositoryPath, int offset = 0, int pageSize = 200, string? reference = null,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        if (offset < 0 || pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
        using var repo = new Repository(repositoryPath);
        var tips = reference is null ? repo.Branches.Select(b => b.Tip).Where(c => c is not null)
            .Concat(repo.Tags.Select(t => t.PeeledTarget as Commit).Where(c => c is not null)).Append(repo.Head.Tip).Where(c => c is not null).DistinctBy(c => c!.Sha).ToArray() :
            [reference == "HEAD" ? repo.Head.Tip : repo.Branches[reference]?.Tip ?? repo.Lookup<Commit>(reference)];
        if (tips.All(c => c is null)) return new GitHistoryPage([], false);
        var labels = GitDisplayReferences.Labels(repo).GroupBy(entry => entry.Sha).ToDictionary(g => g.Key, g => g.Select(e => e.Name).ToArray());
        var commits = repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = tips.Where(t => t is not null), SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time })
            .Skip(offset).Take(pageSize + 1).Select(c =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new GitCommitNode(c.Sha, c.MessageShort, c.Author.Name, c.Author.When, c.Parents.Select(p => p.Sha).ToArray(), labels.GetValueOrDefault(c.Sha, []), c.Author.Email ?? "");
            }).ToArray();
        return new GitHistoryPage(commits.Take(pageSize).ToArray(), commits.Length > pageSize);
    }, cancellationToken);
    public Task<IReadOnlyList<GitCommitNode>> GetAsync(string repositoryPath, int maxCount = 200, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitCommitNode>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (maxCount < 1 || maxCount > 10000)
            throw new ArgumentOutOfRangeException(nameof(maxCount));
        using var repo = new Repository(repositoryPath);
        var labels = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var reference in GitDisplayReferences.Labels(repo))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!labels.TryGetValue(reference.Sha, out var names)) labels[reference.Sha] = names = new List<string>();
            names.Add(reference.Name);
        }
        return repo.Commits.QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time }).Take(maxCount)
            .Select(c => { cancellationToken.ThrowIfCancellationRequested(); return new GitCommitNode(c.Sha, c.MessageShort, c.Author.Name, c.Author.When,
                c.Parents.Select(p => p.Sha).ToArray(),
                labels.TryGetValue(c.Sha, out var names) ? names.OrderBy(n => n, StringComparer.Ordinal).ToArray() : Array.Empty<string>(), c.Author.Email ?? ""); })
            .ToArray();
    }, cancellationToken);
}
