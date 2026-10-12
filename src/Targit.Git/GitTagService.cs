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
}
