using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>Continues or aborts a conflicted cherry-pick or revert without invoking git.exe.</summary>
public sealed class GitSequencerRecoveryService
{
    public Task<string> ContinueAsync(string repositoryPath, string message,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        using var repo = new Repository(repositoryPath);
        if (repo.Info.CurrentOperation is not (CurrentOperation.CherryPick or CurrentOperation.Revert))
            throw new InvalidOperationException("No cherry-pick or revert is in progress.");
        if (repo.Index.Conflicts.Any())
            throw new InvalidOperationException("Resolve all index conflicts first.");
        var signature = new Signature("GitLlama", "gitllama@localhost", DateTimeOffset.UtcNow);
        return repo.Commit(message, signature, signature).Sha;
    }, cancellationToken);

    public Task AbortAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var repo = new Repository(repositoryPath);
        if (repo.Info.CurrentOperation is not (CurrentOperation.CherryPick or CurrentOperation.Revert))
            throw new InvalidOperationException("No cherry-pick or revert is in progress.");
        var head = repo.Head.Tip ?? throw new InvalidOperationException("HEAD is missing.");
        repo.Reset(ResetMode.Hard, head);
    }, cancellationToken);
}
