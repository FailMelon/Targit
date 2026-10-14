namespace GitLlama.Core;

public sealed record RepositoryInfo(string Path, string Name, bool IsFavorite = false);

public sealed record RepositorySnapshot(string Path, string? Branch, string? HeadSha, bool IsBare);

public interface IRepositoryReader
{
    bool IsRepository(string path);
    Task<RepositorySnapshot> ReadAsync(string path, CancellationToken cancellationToken = default);
}
