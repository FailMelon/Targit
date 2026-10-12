using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Local branch operations using LibGit2Sharp, without invoking Git.</summary>
public sealed class GitBranchService
{
    public Task<string> CreateAsync(string repositoryPath, string branchName, bool checkout = false,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare) throw new InvalidOperationException("Branch operations require a working tree.");
        if (repository.Head.Tip is null) throw new InvalidOperationException("Create an initial commit before creating branches.");
        if (!BranchNameIsValid(branchName)) throw new ArgumentException("Invalid branch name.", nameof(branchName));
        if (repository.Branches[branchName] is not null) throw new InvalidOperationException("Branch already exists.");
        cancellationToken.ThrowIfCancellationRequested();
        var branch = repository.CreateBranch(branchName);
        if (checkout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Checkout(repository, branch);
        }
        return branch.FriendlyName;
    }, cancellationToken);

    public Task<string> CheckoutAsync(string repositoryPath, string branchName,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        using var repository = new Repository(repositoryPath);
        if (repository.Info.IsBare) throw new InvalidOperationException("Cannot checkout a bare repository.");
        var branch = repository.Branches[branchName]
            ?? throw new InvalidOperationException("Branch does not exist.");
        if (branch.IsRemote) throw new InvalidOperationException("Select a local branch.");
        cancellationToken.ThrowIfCancellationRequested();
        Commands.Checkout(repository, branch);
        return repository.Head.FriendlyName;
    }, cancellationToken);

    private static bool BranchNameIsValid(string name) =>
        !string.IsNullOrWhiteSpace(name) && !name.StartsWith('-') &&
        !name.StartsWith('/') && !name.EndsWith('/') &&
        !name.EndsWith('.') && !name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("..", StringComparison.Ordinal) && !name.Contains("@{", StringComparison.Ordinal) &&
        !name.Any(c => char.IsControl(c) || " ~^:?*[\\\\ ".Contains(c));
}
