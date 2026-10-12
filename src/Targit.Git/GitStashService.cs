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
    /// <summary>Applies a saved stash without removing it.</summary>
    public Task ApplyAsync(string repositoryPath, int index,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare || repository.Head.Tip is null)
            throw new InvalidOperationException("Stash application requires a working tree and an initial commit.");
        if (repository.RetrieveStatus().IsDirty)
            throw new InvalidOperationException("Commit or stash current changes before applying a stash.");
        if (index >= repository.Stashes.Count())
            throw new ArgumentOutOfRangeException(nameof(index));
        cancellationToken.ThrowIfCancellationRequested();
        var status = repository.Stashes.Apply(index);
        if (status != StashApplyStatus.Applied)
            throw new InvalidOperationException($"Stash application did not succeed: {status}.");
    }, cancellationToken);

}
