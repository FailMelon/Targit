using LibGit2Sharp;
using Targit.Core;

namespace Targit.Git;

/// <summary>Opens Git repositories without invoking the Git CLI.</summary>
public sealed class LibGitRepositoryReader : IRepositoryReader
{
    public bool IsRepository(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try { return Repository.IsValid(Path.GetFullPath(path)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    public Task<RepositorySnapshot> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = Path.GetFullPath(path);
            if (!Repository.IsValid(normalized))
                throw new DirectoryNotFoundException($"Not a Git repository: {normalized}");
            using var repository = new Repository(normalized);
            cancellationToken.ThrowIfCancellationRequested();
            return new RepositorySnapshot(
                normalized,
                repository.Head?.FriendlyName,
                repository.Head?.Tip?.Sha,
                repository.Info.IsBare);
        }, cancellationToken);
    }
}
