using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitIdentity(string? Name, string? Email);

/// <summary>Reads the effective Git commit identity from repository configuration.</summary>
public sealed class GitIdentityService
{
    public Task<GitIdentity> ReadAsync(string repositoryPath,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repository = new Repository(repositoryPath);
        return new GitIdentity(repository.Config.Get<string>("user.name")?.Value,
            repository.Config.Get<string>("user.email")?.Value);
    }, cancellationToken);
    /// <summary>Sets the repository-local commit identity without changing global settings.</summary>
    public Task SetLocalAsync(string repositoryPath, string name, string email,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        using var repository = new Repository(repositoryPath);
        cancellationToken.ThrowIfCancellationRequested();
        repository.Config.Set("user.name", name.Trim(), ConfigurationLevel.Local);
        repository.Config.Set("user.email", email.Trim(), ConfigurationLevel.Local);
    }, cancellationToken);

}
