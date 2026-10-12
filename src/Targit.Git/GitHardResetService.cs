using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Destructive reset of an unpublished local branch to an ancestor.</summary>
public sealed class GitHardResetService
{
    public Task<string> ResetToAncestorAsync(string repositoryPath, string targetSha,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSha);
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare || repository.Info.IsHeadDetached || repository.Head.Tip is not { } head)
            throw new InvalidOperationException("Hard reset requires an attached branch with an existing commit.");
        if (repository.RetrieveStatus().IsDirty)
            throw new InvalidOperationException("Commit or stash local changes before a hard reset.");
        var target = repository.Lookup<Commit>(targetSha)
            ?? throw new ArgumentException("Target commit not found.", nameof(targetSha));
        if (!repository.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = head })
            .Any(commit => commit.Id == target.Id))
            throw new InvalidOperationException("Hard reset target must be an ancestor of current HEAD.");
        foreach (var remoteBranch in repository.Branches.Where(branch => branch.IsRemote && branch.Tip is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (repository.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = remoteBranch.Tip })
                .Any(commit => commit.Id == head.Id))
                throw new InvalidOperationException("HEAD is reachable from remote-tracking history; resetting published history is blocked.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        repository.Reset(ResetMode.Hard, target);
        return repository.Head.Tip!.Sha;
    }, cancellationToken);
}
