namespace GitLlama.Core;

public sealed record CommitSummary(string Sha, string Message, string Author, DateTimeOffset When);

public sealed record ChangedFile(string Path, string State);

public sealed record RepositoryOverview(
    IReadOnlyList<CommitSummary> Commits,
    IReadOnlyList<ChangedFile> Changes,
    IReadOnlyList<string> Branches
);

public interface IRepositoryOverviewReader
{
    Task<RepositoryOverview> ReadOverviewAsync(string path, CancellationToken cancellationToken = default);
}
