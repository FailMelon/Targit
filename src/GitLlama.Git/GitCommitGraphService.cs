using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitCommitNode(string Sha, string Subject, string Author, DateTimeOffset Timestamp, IReadOnlyList<string> Parents, IReadOnlyList<string> Labels);
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
        var labels = repo.Branches.Where(b => b.Tip is not null).Select(b => (b.Tip!.Sha, b.FriendlyName))
            .Concat(repo.Tags.Where(t => t.PeeledTarget is Commit).Select(t => (t.PeeledTarget.Sha, "tag:" + t.FriendlyName)))
            .GroupBy(entry => entry.Sha).ToDictionary(g => g.Key, g => g.Select(e => e.Item2).ToArray());
        var commits = repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = tips.Where(t => t is not null), SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time })
            .Skip(offset).Take(pageSize + 1).Select(c =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new GitCommitNode(c.Sha, c.MessageShort, c.Author.Name, c.Author.When, c.Parents.Select(p => p.Sha).ToArray(), labels.GetValueOrDefault(c.Sha, []));
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
        foreach (var branch in repo.Branches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (branch.Tip is null) continue;
            if (!labels.TryGetValue(branch.Tip.Sha, out var names))
                labels[branch.Tip.Sha] = names = new List<string>();
            names.Add(branch.FriendlyName);
        }
        foreach (var tag in repo.Tags)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var commit = tag.PeeledTarget as Commit;
            if (commit is null) continue;
            if (!labels.TryGetValue(commit.Sha, out var names))
                labels[commit.Sha] = names = new List<string>();
            names.Add("tag:" + tag.FriendlyName);
        }
        return repo.Commits.QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time }).Take(maxCount)
            .Select(c => { cancellationToken.ThrowIfCancellationRequested(); return new GitCommitNode(c.Sha, c.MessageShort, c.Author.Name, c.Author.When,
                c.Parents.Select(p => p.Sha).ToArray(),
                labels.TryGetValue(c.Sha, out var names) ? names.OrderBy(n => n, StringComparer.Ordinal).ToArray() : Array.Empty<string>()); })
            .ToArray();
    }, cancellationToken);
}
