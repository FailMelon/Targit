using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitConflictInfo(string Path, bool HasAncestor, bool HasOurs, bool HasTheirs);

/// <summary>Phase 30 Git operation. Conflicts are left available for explicit user resolution.</summary>
public sealed class GitConflictDetectionService
{
    public Task<IReadOnlyList<GitConflictInfo>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitConflictInfo>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repository = new Repository(repositoryPath);
        return repository.Index.Conflicts
            .Select(conflict => new GitConflictInfo(
                conflict.Ours?.Path ?? conflict.Theirs?.Path ?? conflict.Ancestor?.Path ?? string.Empty,
                conflict.Ancestor is not null,
                conflict.Ours is not null,
                conflict.Theirs is not null))
            .OrderBy(conflict => conflict.Path, StringComparer.Ordinal)
            .ToArray();
    }, cancellationToken);
}
