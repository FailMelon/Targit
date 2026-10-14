using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitStashInfo(int Index, string Message, string Sha = "", string AuthorEmail = "");

/// <summary>
/// What a stash took, and the changes it left in place: unstaged edits and untracked files (only staged changes are
/// stashed), and submodules (Git never stashes work inside them).
/// </summary>
public sealed record GitStashSaveResult(
    int StashedFiles,
    IReadOnlyList<string> Unstaged,
    IReadOnlyList<string> Untracked,
    IReadOnlyList<string> Submodules
)
{
    public bool LeftChanges => Unstaged.Count + Untracked.Count + Submodules.Count > 0;
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

    private const FileStatus StagedStates =
        FileStatus.NewInIndex
        | FileStatus.ModifiedInIndex
        | FileStatus.DeletedFromIndex
        | FileStatus.RenamedInIndex
        | FileStatus.TypeChangeInIndex;

    /// <summary>
    /// Saves only the staged changes in a stash, leaving unstaged edits and untracked files in place. The stash is
    /// authored by the given identity, or by the repository's Git configuration when none is given. LibGit2Sharp
    /// cannot stash the index alone, so this uses system Git (2.35 or newer).
    /// </summary>
    public async Task<GitStashSaveResult> SaveAsync(
        string repositoryPath,
        string message,
        string? authorName = null,
        string? authorEmail = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        var stashesBefore = await Task.Run(
                () =>
                {
                    using var repository = new Repository(repositoryPath);
                    if (repository.Info.IsBare || repository.Head.Tip is null)
                    {
                        throw new InvalidOperationException("A working tree with an initial commit is required.");
                    }

                    if (!repository.RetrieveStatus().Any(entry => (entry.State & StagedStates) != 0))
                    {
                        throw new InvalidOperationException(
                            "There are no staged changes to stash. Stage the changes you want to stash."
                        );
                    }

                    if (
                        (string.IsNullOrWhiteSpace(authorName) || string.IsNullOrWhiteSpace(authorEmail))
                        && repository.Config.BuildSignature(DateTimeOffset.Now) is null
                    )
                    {
                        throw new InvalidOperationException(
                            "Set your name and email in Git or a GitLlama profile before stashing."
                        );
                    }

                    return repository.Stashes.Count();
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        var arguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(authorName) && !string.IsNullOrWhiteSpace(authorEmail))
        {
            arguments.AddRange(["-c", "user.name=" + authorName.Trim(), "-c", "user.email=" + authorEmail.Trim()]);
        }

        arguments.AddRange(["stash", "push", "--staged"]);
        if (!string.IsNullOrWhiteSpace(message))
        {
            arguments.AddRange(["-m", message]);
        }

        await GitSystemTransport
            .RunAsync(repositoryPath, arguments, new GitNetworkOptions(), cancellationToken)
            .ConfigureAwait(false);
        return await Task.Run(
                () =>
                {
                    using var repository = new Repository(repositoryPath);
                    var left = LeftBehind(repository);
                    if (repository.Stashes.Count() == stashesBefore)
                    {
                        throw new InvalidOperationException("Nothing could be stashed. " + Describe(left));
                    }

                    var stash = repository.Stashes.First();
                    var stashed = repository
                        .Diff.Compare<TreeChanges>(stash.Base.Tree, stash.Index.Tree)
                        .Select(change => change.Path)
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
            )
            .ConfigureAwait(false);
    }

    /// <summary>Explains the changes a stash leaves in place, or returns an empty string when there are none.</summary>
    public static string Describe(GitStashSaveResult left)
    {
        var parts = new List<string>();
        if (left.Unstaged.Count > 0)
        {
            parts.Add($"{Count(left.Unstaged.Count, "unstaged file")} (stage changes to stash them)");
        }

        if (left.Untracked.Count > 0)
        {
            parts.Add($"{Count(left.Untracked.Count, "untracked file")} (stage new files to stash them)");
        }

        if (left.Submodules.Count > 0)
        {
            parts.Add($"{Count(left.Submodules.Count, "submodule")} (Git can't stash changes inside submodules)");
        }

        return parts.Count == 0 ? "" : "Left in place: " + string.Join("; ", parts) + ".";
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static GitStashSaveResult LeftBehind(Repository repository)
    {
        var submodules = repository.Submodules.Select(module => module.Path).ToHashSet(StringComparer.Ordinal);
        var remaining = repository
            .RetrieveStatus(new StatusOptions { IncludeIgnored = false })
            .Where(entry => entry.State != FileStatus.Unaltered)
            .Select(entry => (entry.FilePath, entry.State))
            .ToArray();
        return new GitStashSaveResult(
            0,
            remaining
                .Where(entry => !submodules.Contains(entry.FilePath) && entry.State != FileStatus.NewInWorkdir)
                .Select(entry => entry.FilePath)
                .ToArray(),
            remaining
                .Where(entry => !submodules.Contains(entry.FilePath) && entry.State == FileStatus.NewInWorkdir)
                .Select(entry => entry.FilePath)
                .ToArray(),
            remaining.Where(entry => submodules.Contains(entry.FilePath)).Select(entry => entry.FilePath).ToArray()
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
