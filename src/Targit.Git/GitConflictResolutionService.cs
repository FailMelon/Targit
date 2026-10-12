using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Phase 31 Git operation. Conflicts are left available for explicit user resolution.</summary>
public sealed class GitConflictResolutionService
{
    public Task ResolveUsingWorkingTreeAsync(string repositoryPath, string relativePath, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare)
            throw new InvalidOperationException("Conflict resolution requires a working tree.");
        var conflict = repository.Index.Conflicts[relativePath]
            ?? throw new InvalidOperationException("The selected path is not conflicted.");
        var workdir = Path.GetFullPath(repository.Info.WorkingDirectory);
        var absolute = Path.GetFullPath(Path.Combine(workdir, relativePath));
        if (!absolute.StartsWith(workdir, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path must be inside the working tree.", nameof(relativePath));
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Write the resolved file before marking it resolved.", absolute);
        cancellationToken.ThrowIfCancellationRequested();
        Commands.Stage(repository, relativePath);
        if (repository.Index.Conflicts[relativePath] is not null)
            throw new InvalidOperationException("The index still contains a conflict for this path.");
    }, cancellationToken);
}
