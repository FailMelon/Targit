using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitStashInfo(int Index, string Message);

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
                return repository.Stashes.Select((stash, index) => new GitStashInfo(index, stash.Message)).ToArray();
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
    /// Saves only the staged changes in a stash, leaving unstaged and untracked work in place.
    /// LibGit2Sharp cannot stash the index alone, so this uses system Git (2.35 or newer).
    /// </summary>
    public async Task SaveStagedAsync(
        string repositoryPath,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        await Task.Run(
                () =>
                {
                    using var repository = new Repository(repositoryPath);
                    if (repository.Info.IsBare || repository.Head.Tip is null)
                    {
                        throw new InvalidOperationException("A working tree with an initial commit is required.");
                    }

                    if (!repository.RetrieveStatus().Any(entry => (entry.State & StagedStates) != 0))
                    {
                        throw new InvalidOperationException("There are no staged changes to stash.");
                    }
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        string[] arguments = string.IsNullOrWhiteSpace(message)
            ? ["stash", "push", "--staged"]
            : ["stash", "push", "--staged", "-m", message];
        await GitSystemTransport
            .RunAsync(repositoryPath, arguments, new GitNetworkOptions(), cancellationToken)
            .ConfigureAwait(false);
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

    /// <summary>Applies a saved stash without removing it.</summary>
    public Task ApplyAsync(string repositoryPath, int index, CancellationToken cancellationToken = default) =>
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

                if (repository.RetrieveStatus().IsDirty)
                {
                    throw new InvalidOperationException("Commit or stash current changes before applying a stash.");
                }

                if (index >= repository.Stashes.Count())
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                cancellationToken.ThrowIfCancellationRequested();
                var status = repository.Stashes.Apply(index);
                if (status != StashApplyStatus.Applied)
                {
                    throw new InvalidOperationException($"Stash application did not succeed: {status}.");
                }
            },
            cancellationToken
        );

    /// <summary>Applies and removes a stash only if application succeeds.</summary>
    public Task PopAsync(string repositoryPath, int index, CancellationToken cancellationToken = default) =>
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

                if (repository.RetrieveStatus().IsDirty)
                {
                    throw new InvalidOperationException("Commit or stash current changes before popping a stash.");
                }

                if (index >= repository.Stashes.Count())
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                cancellationToken.ThrowIfCancellationRequested();
                var status = repository.Stashes.Pop(index);
                if (status != StashApplyStatus.Applied)
                {
                    throw new InvalidOperationException($"Stash pop did not succeed: {status}.");
                }
            },
            cancellationToken
        );
}
