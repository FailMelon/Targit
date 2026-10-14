using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>Phase 27 Git operation. Conflicts are left available for explicit user resolution.</summary>
public sealed class GitCherryPickService
{
    public Task<string> CherryPickAsync(
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
                    throw new InvalidOperationException("Cherry-pick requires a checked-out branch with a commit.");
                }

                if (repository.RetrieveStatus().IsDirty)
                {
                    throw new InvalidOperationException("Commit or stash local changes before cherry-picking.");
                }

                var commit =
                    repository.Lookup<Commit>(commitSha)
                    ?? throw new ArgumentException("Commit not found.", nameof(commitSha));
                if (commit.Parents.Count() != 1)
                {
                    throw new InvalidOperationException("Cherry-picking root or merge commits is not supported.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var result = repository.CherryPick(
                    commit,
                    new Signature("GitLlama", "gitllama@localhost", DateTimeOffset.UtcNow),
                    new CherryPickOptions { CommitOnSuccess = true }
                );
                if (result.Status != CherryPickStatus.CherryPicked)
                {
                    throw new InvalidOperationException(
                        $"Cherry-pick did not complete: {result.Status}. Resolve conflicts before continuing."
                    );
                }

                return repository.Head.Tip.Sha;
            },
            cancellationToken
        );
}
