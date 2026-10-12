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
    /// <summary>Creates a non-overwriting annotated tag on the current commit.</summary>
    public Task CreateAnnotatedAsync(string repositoryPath, string tagName, string message,
        string authorName, string authorEmail, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorEmail);
        using var repository = new Repository(repositoryPath);
        if (repository.Head.Tip is null)
            throw new InvalidOperationException("An existing commit is required to create a tag.");
        if (repository.Tags[tagName] is not null)
            throw new InvalidOperationException($"Tag '{tagName}' already exists.");
        var signature = new Signature(authorName, authorEmail, DateTimeOffset.UtcNow);
        cancellationToken.ThrowIfCancellationRequested();
        repository.Tags.Add(tagName, repository.Head.Tip, signature, message, false);
    }, cancellationToken);

}
