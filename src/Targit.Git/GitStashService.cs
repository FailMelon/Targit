using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitStashInfo(int Index, string Message);

/// <summary>Lists saved stashes without applying or deleting them.</summary>
public sealed class GitStashService
{
    public Task<IReadOnlyList<GitStashInfo>> ListAsync(string repositoryPath,
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitStashInfo>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repository = new Repository(repositoryPath);
        return repository.Stashes.Select((stash, index) =>
            new GitStashInfo(index, stash.Message)).ToArray();
    }, cancellationToken);
    /// <summary>Permanently removes a selected stash entry without applying it.</summary>
    public Task DropAsync(string repositoryPath, int index,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        using var repository = new Repository(repositoryPath);
        if (index >= repository.Stashes.Count())
            throw new ArgumentOutOfRangeException(nameof(index), "Stash does not exist.");
        cancellationToken.ThrowIfCancellationRequested();
        repository.Stashes.Remove(index);
    }, cancellationToken);

}
