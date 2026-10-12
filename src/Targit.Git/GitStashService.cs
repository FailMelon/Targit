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
    /// <summary>Applies and removes a stash only if application succeeds.</summary>
    public Task PopAsync(string repositoryPath, int index,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare || repository.Head.Tip is null)
            throw new InvalidOperationException("Stash pop requires a working tree and an initial commit.");
        if (repository.RetrieveStatus().IsDirty)
            throw new InvalidOperationException("Commit or stash current changes before popping a stash.");
        if (index >= repository.Stashes.Count())
            throw new ArgumentOutOfRangeException(nameof(index));
        cancellationToken.ThrowIfCancellationRequested();
        var status = repository.Stashes.Pop(index);
        if (status != StashApplyStatus.Applied)
            throw new InvalidOperationException($"Stash pop did not succeed: {status}.");
    }, cancellationToken);

}
