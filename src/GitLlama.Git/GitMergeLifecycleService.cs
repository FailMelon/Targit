using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>Finishes or abandons a merge that stopped for conflicts.</summary>
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

    /// <summary>
    /// Commits a merge whose conflicts are all resolved. A blank message uses the one Git prepared for the merge;
    /// the commit is authored by the given identity, or by the repository's Git configuration when none is given.
    /// </summary>
    public Task<string> ContinueAsync(
        string repositoryPath,
        string? message,
        string? authorName = null,
        string? authorEmail = null,
        CancellationToken cancellationToken = default
    ) =>
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

                if (repo.Index.Conflicts.Any())
                {
                    throw new InvalidOperationException("Resolve all conflicts before continuing.");
                }

                var text = string.IsNullOrWhiteSpace(message) ? PreparedMessage(repo) : message.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("Enter a merge commit message.");
                }

                var signature =
                    !string.IsNullOrWhiteSpace(authorName) && !string.IsNullOrWhiteSpace(authorEmail)
                        ? new Signature(authorName.Trim(), authorEmail.Trim(), DateTimeOffset.Now)
                        : repo.Config.BuildSignature(DateTimeOffset.Now)
                            ?? throw new InvalidOperationException(
                                "Set your name and email in Git or a GitLlama profile before finishing the merge."
                            );
                cancellationToken.ThrowIfCancellationRequested();
                return repo.Commit(text, signature, signature).Sha;
            },
            cancellationToken
        );

    /// <summary>The message Git prepared in MERGE_MSG, without its comment lines.</summary>
    private static string PreparedMessage(Repository repo) =>
        string.Join(
                "\n",
                (repo.Info.Message ?? "")
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Split('\n')
                    .Where(line => !line.StartsWith('#'))
            )
            .Trim();
}
