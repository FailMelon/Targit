using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitCommitNode(string Sha, string Subject, string Author, DateTimeOffset Timestamp, IReadOnlyList<string> Parents, IReadOnlyList<string> Labels);

/// <summary>Phase 36 Git backend service.</summary>
public sealed class GitCommitGraphService
{
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
            if (branch.Tip is null) continue;
            if (!labels.TryGetValue(branch.Tip.Sha, out var names))
                labels[branch.Tip.Sha] = names = new List<string>();
            names.Add(branch.FriendlyName);
        }
        foreach (var tag in repo.Tags)
        {
            if (tag.Target is not Commit commit) continue;
            if (!labels.TryGetValue(commit.Sha, out var names))
                labels[commit.Sha] = names = new List<string>();
            names.Add("tag:" + tag.FriendlyName);
        }
        return repo.Commits.Take(maxCount)
            .Select(c => new GitCommitNode(c.Sha, c.MessageShort, c.Author.Name, c.Author.When,
                c.Parents.Select(p => p.Sha).ToArray(),
                labels.TryGetValue(c.Sha, out var names) ? names.OrderBy(n => n, StringComparer.Ordinal).ToArray() : Array.Empty<string>()))
            .ToArray();
    }, cancellationToken);
}
