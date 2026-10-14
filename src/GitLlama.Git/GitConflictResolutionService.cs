using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>Phase 31 Git operation. Conflicts are left available for explicit user resolution.</summary>
public sealed class GitConflictResolutionService
{
    public Task WriteAndResolveAsync(
        string repositoryPath,
        string relativePath,
        string content,
        CancellationToken ct = default
    ) =>
        Task.Run(
            () =>
            {
                using var repo = new Repository(repositoryPath);
                if (repo.Info.IsBare)
                {
                    throw new InvalidOperationException("A working tree is required.");
                }

                var conflict =
                    repo.Index.Conflicts[relativePath]
                    ?? throw new InvalidOperationException("This file is no longer conflicted.");
                if (
                    new[] { conflict.Ancestor, conflict.Ours, conflict.Theirs }.Any(entry =>
                        entry is not null && entry.Mode is not (Mode.NonExecutableFile or Mode.ExecutableFile)
                    )
                )
                {
                    throw new InvalidOperationException("The result editor supports regular text files.");
                }

                var absolute = GitPathGuard.RegularWorkingFile(repo, relativePath);
                if (content.Contains('\0'))
                {
                    throw new InvalidOperationException("Resolve binary files by choosing ours or theirs.");
                }

                ct.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
                File.WriteAllText(absolute, content, new System.Text.UTF8Encoding(false));
                Commands.Stage(repo, relativePath);
            },
            ct
        );

    public Task ResolveUsingWorkingTreeAsync(
        string repositoryPath,
        string relativePath,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
                using var repository = new Repository(repositoryPath);
                if (repository.Info.IsBare)
                {
                    throw new InvalidOperationException("Conflict resolution requires a working tree.");
                }

                var conflict =
                    repository.Index.Conflicts[relativePath]
                    ?? throw new InvalidOperationException("The selected path is not conflicted.");
                var absolute = GitPathGuard.RegularWorkingFile(repository, relativePath);
                if (!File.Exists(absolute))
                {
                    throw new FileNotFoundException("Write the resolved file before marking it resolved.", absolute);
                }

                cancellationToken.ThrowIfCancellationRequested();
                Commands.Stage(repository, relativePath);
                if (repository.Index.Conflicts[relativePath] is not null)
                {
                    throw new InvalidOperationException("The index still contains a conflict for this path.");
                }
            },
            cancellationToken
        );
}
