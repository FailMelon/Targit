using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>Phase 32 Git backend service.</summary>
public sealed class GitMergeLifecycleService
{
    public Task AbortAsync(string repositoryPath, CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                using var repo = new Repository(repositoryPath);
                if (repo.Info.CurrentOperation != CurrentOperation.Merge)
                {
                    throw new InvalidOperationException("No merge is in progress.");
                }

                if (repo.Head.Tip is null)
                {
                    throw new InvalidOperationException("Cannot abort without HEAD.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                repo.Reset(ResetMode.Hard, repo.Head.Tip);
            },
            cancellationToken
        );

    public Task<string> ContinueAsync(
        string repositoryPath,
        string message,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                ArgumentException.ThrowIfNullOrWhiteSpace(message);
                using var repo = new Repository(repositoryPath);
                if (repo.Info.CurrentOperation != CurrentOperation.Merge)
                {
                    throw new InvalidOperationException("No merge is in progress.");
                }

                if (repo.Index.Conflicts.Any())
                {
                    throw new InvalidOperationException("Resolve all conflicts before continuing.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var signature = new Signature("GitLlama", "gitllama@localhost", DateTimeOffset.UtcNow);
                return repo.Commit(message, signature, signature).Sha;
            },
            cancellationToken
        );
}
