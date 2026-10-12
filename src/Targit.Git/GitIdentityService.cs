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
}
