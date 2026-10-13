using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>Amends the latest local commit. Rewriting a published commit may require coordination.</summary>
public sealed class GitAmendService
{
    public Task<string> AmendMessageAsync(string repositoryPath, string newMessage,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newMessage);
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare || repository.Info.IsHeadDetached || repository.Head.Tip is null)
            throw new InvalidOperationException("Amend requires a checked-out local branch with a commit.");
        if (repository.RetrieveStatus().IsDirty)
            throw new InvalidOperationException("Commit or stash local changes before amending the message.");
        var original = repository.Head.Tip;
        if (repository.Branches.Any(branch => branch.IsRemote && branch.Tip?.Sha == original.Sha))
            throw new InvalidOperationException("The current commit is referenced by a fetched remote branch; refusing to rewrite published history.");
        cancellationToken.ThrowIfCancellationRequested();
        var amended = repository.Commit(newMessage, original.Author, original.Committer,
            new CommitOptions { AmendPreviousCommit = true, AllowEmptyCommit = true });
        return amended.Sha;
    }, cancellationToken);
}
