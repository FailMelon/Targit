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
    /// <summary>Saves tracked working-tree changes in a stash without including untracked files.</summary>
    public Task SaveAsync(string repositoryPath, string message,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare || repository.Head.Tip is null)
            throw new InvalidOperationException("A working tree with an initial commit is required.");
        if (!repository.RetrieveStatus().IsDirty)
            throw new InvalidOperationException("There are no local changes to stash.");
        var signature = new Signature("Targit", "targit@localhost", DateTimeOffset.UtcNow);
        cancellationToken.ThrowIfCancellationRequested();
        repository.Stashes.Add(signature, message ?? string.Empty, StashModifiers.Default);
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
