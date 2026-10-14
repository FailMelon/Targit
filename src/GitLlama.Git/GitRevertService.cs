using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>Phase 28 Git operation. Conflicts are left available for explicit user resolution.</summary>
public sealed class GitRevertService
{
    public Task<string> RevertAsync(
        string repositoryPath,
        string commitSha,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
                using var repository = new Repository(repositoryPath);
                if (repository.Info.IsBare || repository.Info.IsHeadDetached || repository.Head.Tip is null)
                {
                    throw new InvalidOperationException("Revert requires a checked-out branch with a commit.");
                }

                if (repository.RetrieveStatus().IsDirty)
                {
                    throw new InvalidOperationException("Commit or stash local changes before reverting.");
                }

                var commit =
                    repository.Lookup<Commit>(commitSha)
                    ?? throw new ArgumentException("Commit not found.", nameof(commitSha));
                if (commit.Parents.Count() != 1)
                {
                    throw new InvalidOperationException("Reverting root or merge commits is not supported.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var result = repository.Revert(
                    commit,
                    new Signature("GitLlama", "gitllama@localhost", DateTimeOffset.UtcNow),
                    new RevertOptions { CommitOnSuccess = true }
                );
                if (result.Status != RevertStatus.Reverted)
                {
                    throw new InvalidOperationException(
                        $"Revert did not complete: {result.Status}. Resolve conflicts before continuing."
                    );
                }

                return repository.Head.Tip.Sha;
            },
            cancellationToken
        );
}
