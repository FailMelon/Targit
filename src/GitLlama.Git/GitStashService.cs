using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitStashInfo(int Index, string Message, string Sha = "", string AuthorEmail = "");

/// <summary>
/// What a stash took, and the listed changes it had to leave behind: submodules (Git never stashes work inside
/// them), untracked files when they weren't included, and files whose only difference is line endings.
/// </summary>
public sealed record GitStashSaveResult(
    int StashedFiles,
    IReadOnlyList<string> Submodules,
    IReadOnlyList<string> Untracked,
    IReadOnlyList<string> LineEndingsOnly
)
{
    public bool LeftChanges => Submodules.Count + Untracked.Count + LineEndingsOnly.Count > 0;
}

/// <summary>Lists saved stashes without applying or deleting them.</summary>
public sealed class GitStashService
{
    public Task<IReadOnlyList<GitStashInfo>> ListAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run<IReadOnlyList<GitStashInfo>>(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                using var repository = new Repository(repositoryPath);
                return repository
                    .Stashes.Select(
                        (stash, index) =>
                            new GitStashInfo(index, stash.Message, stash.WorkTree.Sha, stash.WorkTree.Author.Email)
                    )
                    .ToArray();
            },
            cancellationToken
        );

    /// <summary>
    /// Saves tracked working-tree changes in a stash, and untracked files too when asked. Ignored files are never
    /// stashed. The stash is authored by the given identity, or by the repository's Git configuration when none is
    /// given.
    /// </summary>
    public Task<GitStashSaveResult> SaveAsync(
        string repositoryPath,
        string message,
        string? authorName = null,
        string? authorEmail = null,
        bool includeUntracked = false,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                using var repository = new Repository(repositoryPath);
                if (repository.Info.IsBare || repository.Head.Tip is null)
                {
                    throw new InvalidOperationException("A working tree with an initial commit is required.");
                }

                var changes = repository
                    .RetrieveStatus(new StatusOptions { IncludeIgnored = false })
                    .Where(entry => entry.State != FileStatus.Unaltered)
                    .ToArray();
                if (changes.Length == 0)
                {
                    throw new InvalidOperationException("There are no local changes to stash.");
                }

                if (!includeUntracked && changes.All(entry => entry.State == FileStatus.NewInWorkdir))
                {
                    throw new InvalidOperationException(
                        "Only untracked files have changed. Turn on Include untracked files to stash them."
                    );
                }

                var signature =
                    !string.IsNullOrWhiteSpace(authorName) && !string.IsNullOrWhiteSpace(authorEmail)
                        ? new Signature(authorName.Trim(), authorEmail.Trim(), DateTimeOffset.Now)
                        : repository.Config.BuildSignature(DateTimeOffset.Now)
                            ?? throw new InvalidOperationException(
                                "Set your name and email in Git or a GitLlama profile before stashing."
                            );
                cancellationToken.ThrowIfCancellationRequested();
                var stash = repository.Stashes.Add(
                    signature,
                    message ?? string.Empty,
                    includeUntracked ? StashModifiers.IncludeUntracked : StashModifiers.Default
                );
                var left = LeftBehind(repository, includeUntracked);
                if (stash is null)
                {
                    throw new InvalidOperationException("Nothing could be stashed. " + Describe(left));
                }

                var stashed = repository
                    .Diff.Compare<TreeChanges>(stash.Base.Tree, stash.Index.Tree)
                    .Concat(repository.Diff.Compare<TreeChanges>(stash.Base.Tree, stash.WorkTree.Tree))
                    .Select(change => change.Path)
                    .Concat(stash.Untracked?.Tree.Select(entry => entry.Path) ?? [])
                    .Distinct()
                    .Count();
                if (stashed == 0)
                {
                    // Git can record a stash that changes nothing, for example when files differ only in line
                    // endings. Remove it rather than leave an empty entry behind.
                    repository.Stashes.Remove(0);
                    throw new InvalidOperationException("Nothing could be stashed. " + Describe(left));
                }

                return left with
                {
                    StashedFiles = stashed,
                };
            },
            cancellationToken
        );

    /// <summary>Explains the changes a stash leaves in place, or returns an empty string when there are none.</summary>
    public static string Describe(GitStashSaveResult left)
    {
        var parts = new List<string>();
        if (left.Submodules.Count > 0)
        {
            parts.Add($"{Count(left.Submodules.Count, "submodule")} (Git can't stash changes inside submodules)");
        }

        if (left.Untracked.Count > 0)
        {
            parts.Add(
                $"{Count(left.Untracked.Count, "untracked file")} (turn on Include untracked files to stash them)"
            );
        }

        if (left.LineEndingsOnly.Count > 0)
        {
            parts.Add(
                $"{Count(left.LineEndingsOnly.Count, "file")} with only line-ending differences, so there's nothing to stash"
            );
        }

        return parts.Count == 0 ? "" : "Left in place: " + string.Join("; ", parts) + ".";
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static GitStashSaveResult LeftBehind(Repository repository, bool includeUntracked)
    {
        var submodules = repository.Submodules.Select(module => module.Path).ToHashSet(StringComparer.Ordinal);
        var remaining = repository
            .RetrieveStatus(new StatusOptions { IncludeIgnored = false })
            .Where(entry => entry.State != FileStatus.Unaltered)
            .Select(entry => (entry.FilePath, entry.State))
            .ToArray();
        return new GitStashSaveResult(
            0,
            remaining.Where(entry => submodules.Contains(entry.FilePath)).Select(entry => entry.FilePath).ToArray(),
            remaining
                .Where(entry => !submodules.Contains(entry.FilePath) && entry.State == FileStatus.NewInWorkdir)
                .Select(entry => entry.FilePath)
                .ToArray(),
            remaining
                .Where(entry => !submodules.Contains(entry.FilePath) && entry.State != FileStatus.NewInWorkdir)
                .Select(entry => entry.FilePath)
                .ToArray()
        );
    }

    /// <summary>Permanently removes a selected stash entry without applying it.</summary>
    public Task DropAsync(string repositoryPath, int index, CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                using var repository = new Repository(repositoryPath);
                if (index >= repository.Stashes.Count())
                {
                    throw new ArgumentOutOfRangeException(nameof(index), "Stash does not exist.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                repository.Stashes.Remove(index);
            },
            cancellationToken
        );

    /// <summary>
    /// Applies a saved stash without removing it. With <paramref name="allowLocalChanges"/>, the stash is applied on
    /// top of uncommitted changes; it is refused without touching anything if it would overwrite them.
    /// </summary>
    public Task ApplyAsync(
        string repositoryPath,
        int index,
        bool allowLocalChanges = false,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                using var repository = new Repository(repositoryPath);
                if (repository.Info.IsBare || repository.Head.Tip is null)
                {
                    throw new InvalidOperationException(
                        "Stash application requires a working tree and an initial commit."
                    );
                }

                if (!allowLocalChanges && repository.RetrieveStatus().IsDirty)
                {
                    throw new InvalidOperationException("Commit or stash current changes before applying a stash.");
                }

                if (index >= repository.Stashes.Count())
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                cancellationToken.ThrowIfCancellationRequested();
                Restore(() => repository.Stashes.Apply(index), "Stash application");
            },
            cancellationToken
        );

    /// <summary>
    /// Applies and removes a stash only if application succeeds. With <paramref name="allowLocalChanges"/>, the stash
    /// is applied on top of uncommitted changes; it is refused and kept if it would overwrite them.
    /// </summary>
    public Task PopAsync(
        string repositoryPath,
        int index,
        bool allowLocalChanges = false,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                using var repository = new Repository(repositoryPath);
                if (repository.Info.IsBare || repository.Head.Tip is null)
                {
                    throw new InvalidOperationException("Stash pop requires a working tree and an initial commit.");
                }

                if (!allowLocalChanges && repository.RetrieveStatus().IsDirty)
                {
                    throw new InvalidOperationException("Commit or stash current changes before popping a stash.");
                }

                if (index >= repository.Stashes.Count())
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                cancellationToken.ThrowIfCancellationRequested();
                Restore(() => repository.Stashes.Pop(index), "Stash pop");
            },
            cancellationToken
        );

    private static void Restore(Func<StashApplyStatus> restore, string operation)
    {
        StashApplyStatus status;
        try
        {
            status = restore();
        }
        catch (CheckoutConflictException)
        {
            status = StashApplyStatus.Conflicts;
        }

        if (status == StashApplyStatus.Conflicts)
        {
            throw new InvalidOperationException(
                "The stash changes files you've also changed. Commit or stash your changes first; the stash was kept."
            );
        }

        if (status != StashApplyStatus.Applied)
        {
            throw new InvalidOperationException($"{operation} did not succeed: {status}.");
        }
    }
}
