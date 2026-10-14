using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitCommitChangedFile(string Path, string Status);

public sealed class GitCommitChangedFilesService
{
    public Task<IReadOnlyList<GitCommitChangedFile>> GetAsync(
        string repositoryPath,
        string commitSha,
        int maxCount = 200,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run<IReadOnlyList<GitCommitChangedFile>>(
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
                var changes = repository.Diff.Compare<TreeChanges>(parent?.Tree, commit.Tree);
                cancellationToken.ThrowIfCancellationRequested();
                return changes
                    .OrderBy(change => change.Path, StringComparer.Ordinal)
                    .Take(maxCount)
                    .Select(change => new GitCommitChangedFile(change.Path, change.Status.ToString()))
                    .ToArray();
            },
            cancellationToken
        );
}
