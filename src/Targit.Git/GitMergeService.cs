using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Phase 29 Git operation. Conflicts are left available for explicit user resolution.</summary>
public sealed class GitMergeService
{
    public Task<MergeStatus> MergeAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare || repository.Info.IsHeadDetached || repository.Head.Tip is null)
            throw new InvalidOperationException("Merge requires a checked-out branch.");
        if (repository.RetrieveStatus().IsDirty)
            throw new InvalidOperationException("Commit or stash local changes before merging.");
        var branch = repository.Branches[branchName]
            ?? throw new ArgumentException("Branch not found.", nameof(branchName));
        if (branch.IsRemote)
            throw new InvalidOperationException("Merge a local branch or create a local tracking branch first.");
        cancellationToken.ThrowIfCancellationRequested();
        var result = repository.Merge(branch, new Signature("Targit", "targit@localhost", DateTimeOffset.UtcNow),
            new MergeOptions { FastForwardStrategy = FastForwardStrategy.Default });
        return result.Status;
    }, cancellationToken);
}
