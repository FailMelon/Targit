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
}
