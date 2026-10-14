using System.Text.Json.Serialization;

namespace GitLlama.Core;

public sealed record RepositoryInfo(string Path, string Name, bool IsFavorite = false)
{
    /// <summary>The repository this one was opened from as a submodule, or null for a top-level repository.</summary>
    [JsonIgnore]
    public RepositoryInfo? Parent { get; init; }

    /// <summary>The top-level repository, following submodule parents.</summary>
    [JsonIgnore]
    public RepositoryInfo Root => Parent?.Root ?? this;
}

public sealed record RepositorySnapshot(string Path, string? Branch, string? HeadSha, bool IsBare);

public interface IRepositoryReader
{
    bool IsRepository(string path);
    Task<RepositorySnapshot> ReadAsync(string path, CancellationToken cancellationToken = default);
}
