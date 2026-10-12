using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitRebaseConflictState(bool IsRebasing, IReadOnlyList<string> Paths);

/// <summary>Phase 35 Git backend service.</summary>
public sealed class GitRebaseConflictService
{
    public Task<GitRebaseConflictState> InspectAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repo = new Repository(repositoryPath);
        var paths = repo.Index.Conflicts
            .Select(c => c.Ours?.Path ?? c.Theirs?.Path ?? c.Ancestor?.Path ?? string.Empty)
            .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        return new GitRebaseConflictState(repo.Info.CurrentOperation == CurrentOperation.Rebase, paths);
    }, cancellationToken);
}
