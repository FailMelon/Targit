using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitCommitFileStatistic(string Path, string Status, int LinesAdded, int LinesDeleted);

public sealed class GitCommitFileStatisticsService
{
    public Task<IReadOnlyList<GitCommitFileStatistic>> GetAsync(
        string repositoryPath,
        string commitSha,
        int maxCount = 200,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run<IReadOnlyList<GitCommitFileStatistic>>(
            () =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
                if (maxCount is < 1 or > 2000)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxCount));
                }

                cancellationToken.ThrowIfCancellationRequested();
                using var repository = new Repository(repositoryPath);
                var commit =
                    repository.Lookup<Commit>(commitSha.Trim())
                    ?? throw new ArgumentException("Commit not found.", nameof(commitSha));
                cancellationToken.ThrowIfCancellationRequested();
                var parent = commit.Parents.FirstOrDefault();
                var patch = repository.Diff.Compare<Patch>(parent?.Tree, commit.Tree);
                cancellationToken.ThrowIfCancellationRequested();
                return patch
                    .OrderBy(entry => entry.Path, StringComparer.Ordinal)
                    .Take(maxCount)
                    .Select(entry => new GitCommitFileStatistic(
                        entry.Path,
                        entry.Status.ToString(),
                        entry.LinesAdded,
                        entry.LinesDeleted
                    ))
                    .ToArray();
            },
            cancellationToken
        );
}
