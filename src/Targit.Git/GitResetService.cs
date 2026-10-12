using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Phase 33 Git backend service.</summary>
public sealed class GitResetService
{
    public Task ResetAsync(string repositoryPath, string targetSha, ResetMode mode, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSha);
        using var repo = new Repository(repositoryPath);
        if (repo.Info.IsBare || repo.Info.IsHeadDetached || repo.Head.Tip is null)
            throw new InvalidOperationException("Reset requires a checked-out local branch.");
        if (repo.Info.CurrentOperation != CurrentOperation.None)
            throw new InvalidOperationException("Complete the current Git operation before resetting.");
        var target = repo.Lookup<Commit>(targetSha)
            ?? throw new ArgumentException("Target commit does not exist.", nameof(targetSha));
        cancellationToken.ThrowIfCancellationRequested();
        repo.Reset(mode, target);
    }, cancellationToken);
}
