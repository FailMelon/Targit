using LibGit2Sharp;
using Targit.Core;
namespace Targit.Git;
public sealed class LibGitRepositoryReader : IRepositoryReader
{
    public bool IsRepository(string path) => !string.IsNullOrWhiteSpace(path) && Repository.IsValid(path);
    public Task<RepositorySnapshot> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Repository.IsValid(path)) throw new DirectoryNotFoundException($"Not a Git repository: {path}");
            using var repository = new Repository(path);
            return new RepositorySnapshot(path, repository.Head?.FriendlyName, repository.Head?.Tip?.Sha, repository.Info.IsBare);
        }, cancellationToken);
    }
}