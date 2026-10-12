using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitTagInfo(string Name, string TargetSha, bool IsAnnotated);

/// <summary>Reads repository tags without modifying history.</summary>
public sealed class GitTagService
{
    public Task<IReadOnlyList<GitTagInfo>> ListAsync(string repositoryPath,
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitTagInfo>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repository = new Repository(repositoryPath);
        return repository.Tags
            .Select(tag => new GitTagInfo(tag.FriendlyName, tag.Target.Sha,
                tag.Annotation is not null))
            .OrderBy(tag => tag.Name, StringComparer.Ordinal)
            .ToArray();
    }, cancellationToken);
    /// <summary>Creates a lightweight tag at the current commit without overwriting existing tags.</summary>
    public Task CreateAsync(string repositoryPath, string tagName,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        using var repository = new Repository(repositoryPath);
        if (repository.Head.Tip is null)
            throw new InvalidOperationException("An existing commit is required to create a tag.");
        if (repository.Tags[tagName] is not null)
            throw new InvalidOperationException($"Tag '{tagName}' already exists.");
        cancellationToken.ThrowIfCancellationRequested();
        repository.Tags.Add(tagName, repository.Head.Tip, false);
    }, cancellationToken);


    /// <summary>Pushes one existing local tag to the selected remote without forcing.</summary>
    public Task PushAsync(string repositoryPath, string remoteName, string tagName,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        using var repository = new Repository(repositoryPath);
        var remote = repository.Network.Remotes[remoteName]
            ?? throw new InvalidOperationException($"Remote '{remoteName}' does not exist.");
        var tag = repository.Tags[tagName]
            ?? throw new InvalidOperationException($"Tag '{tagName}' does not exist.");
        cancellationToken.ThrowIfCancellationRequested();
        repository.Network.Push(remote, $"refs/tags/{tag.FriendlyName}:refs/tags/{tag.FriendlyName}",
            new PushOptions());
    }, cancellationToken);

    /// <summary>Deletes one tag on the remote without deleting the local tag.</summary>
    public Task DeleteRemoteAsync(string repositoryPath, string remoteName, string tagName,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        using var repository = new Repository(repositoryPath);
        var remote = repository.Network.Remotes[remoteName]
            ?? throw new InvalidOperationException($"Remote '{remoteName}' does not exist.");
        cancellationToken.ThrowIfCancellationRequested();
        repository.Network.Push(remote, $":refs/tags/{tagName}", new PushOptions());
    }, cancellationToken);
}
