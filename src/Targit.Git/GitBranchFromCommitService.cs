using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Creates a local branch at a selected existing commit without changing HEAD or the working tree.</summary>
public sealed class GitBranchFromCommitService
{
    public Task<string> CreateAsync(string repositoryPath, string commitSha, string branchName,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare)
            throw new InvalidOperationException("Creating a branch from history requires a working repository.");
        var commit = repository.Lookup<Commit>(commitSha)
            ?? throw new ArgumentException("Selected commit was not found.", nameof(commitSha));
        var name = branchName.Trim();
        if (name.StartsWith('-') || name.StartsWith('/') || name.EndsWith('/') || name.EndsWith('.') ||
            name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) || name.Contains("..", StringComparison.Ordinal) ||
            name.Contains("@{", StringComparison.Ordinal) || name.Contains("//", StringComparison.Ordinal) ||
            name.Any(c => char.IsControl(c) || " ~^:?*[\\\\ ".Contains(c)))
            throw new ArgumentException("Invalid branch name.", nameof(branchName));
        if (repository.Branches["refs/heads/" + name] is not null)
            throw new InvalidOperationException("A local branch with this name already exists.");
        cancellationToken.ThrowIfCancellationRequested();
        return repository.CreateBranch(name, commit).FriendlyName;
    }, cancellationToken);
}
