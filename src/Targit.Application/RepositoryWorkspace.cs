using Targit.Core;
namespace Targit.Application;
public sealed class RepositoryWorkspace(IRepositoryReader reader)
{
    public Task<RepositorySnapshot> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A repository path is required", nameof(path));
        return reader.ReadAsync(Path.GetFullPath(path), cancellationToken);
    }
}