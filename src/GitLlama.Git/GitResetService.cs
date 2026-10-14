using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>Resets the current local branch to an ancestor, guarding published history and local changes.</summary>
public sealed class GitResetService
{
    public Task ResetAsync(
        string repositoryPath,
        string targetSha,
        ResetMode mode,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                ArgumentException.ThrowIfNullOrWhiteSpace(targetSha);
                if (mode is not (ResetMode.Soft or ResetMode.Mixed or ResetMode.Hard))
                {
                    throw new ArgumentOutOfRangeException(nameof(mode));
                }

                using var repo = new Repository(repositoryPath);
                if (repo.Info.IsBare || repo.Info.IsHeadDetached || repo.Head.Tip is not { } head)
                {
                    throw new InvalidOperationException("Reset requires a checked-out local branch.");
                }

                if (repo.Info.CurrentOperation != CurrentOperation.None)
                {
                    throw new InvalidOperationException("Complete the current Git operation before resetting.");
                }

                if (repo.RetrieveStatus().IsDirty)
                {
                    throw new InvalidOperationException("Commit or stash local changes before resetting.");
                }

                var target =
                    repo.Lookup<Commit>(targetSha)
                    ?? throw new ArgumentException("Target commit does not exist.", nameof(targetSha));
                if (
                    !repo
                        .Commits.QueryBy(new CommitFilter { IncludeReachableFrom = head })
                        .Any(commit => commit.Id == target.Id)
                )
                {
                    throw new InvalidOperationException("Reset target must be an ancestor of HEAD.");
                }

                foreach (var remote in repo.Branches.Where(branch => branch.IsRemote && branch.Tip is not null))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (
                        repo
                            .Commits.QueryBy(new CommitFilter { IncludeReachableFrom = remote.Tip })
                            .Any(commit => commit.Id == head.Id)
                    )
                    {
                        throw new InvalidOperationException("Resetting published HEAD history is blocked.");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                repo.Reset(mode, target);
            },
            cancellationToken
        );
}
