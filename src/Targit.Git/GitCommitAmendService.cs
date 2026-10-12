using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Changes only the latest local commit message, refusing published history.</summary>
public sealed class GitCommitAmendService
{
    public Task<string> AmendLatestMessageAsync(string repositoryPath, string message,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare || repository.Head.IsDetached || repository.Head.Tip is not { } tip)
            throw new InvalidOperationException("An attached branch with an existing commit is required.");
        if (repository.RetrieveStatus().IsDirty)
            throw new InvalidOperationException("Commit or stash all changes before amending the commit message.");
        foreach (var remoteBranch in repository.Branches.Where(branch => branch.IsRemote && branch.Tip is not null))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (repository.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = remoteBranch.Tip })
                .Any(commit => commit.Id == tip.Id))
                throw new InvalidOperationException("The latest commit is reachable from a remote-tracking branch; rewriting published history is blocked.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        var amended = repository.Commit(message.Trim(), tip.Author, tip.Committer,
            new CommitOptions { AmendPreviousCommit = true });
        return amended.Sha;
    }, cancellationToken);
}
