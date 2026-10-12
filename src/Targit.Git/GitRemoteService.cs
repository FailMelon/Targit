using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitRemoteInfo(string Name, string Url, string PushUrl);

/// <summary>Manages Git remote configuration using LibGit2Sharp.</summary>
public sealed class GitRemoteService
{
    public Task<IReadOnlyList<GitRemoteInfo>> ListAsync(string repositoryPath,
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitRemoteInfo>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repository = new Repository(repositoryPath);
        return repository.Network.Remotes
            .Select(remote => new GitRemoteInfo(remote.Name, remote.Url, remote.PushUrl ?? remote.Url))
            .OrderBy(remote => remote.Name, StringComparer.Ordinal)
            .ToArray();
    }, cancellationToken);

    public Task AddAsync(string repositoryPath, string name, string url,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        using var repository = new Repository(repositoryPath);
        if (repository.Network.Remotes[name] is not null)
            throw new InvalidOperationException($"Remote '{name}' already exists.");
        cancellationToken.ThrowIfCancellationRequested();
        repository.Network.Remotes.Add(name, url);
    }, cancellationToken);

    public Task FetchAsync(string repositoryPath, string name,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var repository = new Repository(repositoryPath);
        var remote = repository.Network.Remotes[name]
            ?? throw new InvalidOperationException($"Remote '{name}' does not exist.");
        var options = new FetchOptions
        {
            OnTransferProgress = progress => !cancellationToken.IsCancellationRequested
        };
        cancellationToken.ThrowIfCancellationRequested();
        Commands.Fetch(repository, remote.Name, remote.FetchRefSpecs.Select(spec => spec.Specification),
            options, $"Targit fetch {name}");
        cancellationToken.ThrowIfCancellationRequested();
    }, cancellationToken);

    public Task RemoveAsync(string repositoryPath, string name,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var repository = new Repository(repositoryPath);
        if (repository.Network.Remotes[name] is null)
            throw new InvalidOperationException($"Remote '{name}' does not exist.");
        cancellationToken.ThrowIfCancellationRequested();
        repository.Network.Remotes.Remove(name);
    }, cancellationToken);
}
