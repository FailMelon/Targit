using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Local working-tree actions. No external Git process is invoked.</summary>
public sealed class GitWorkingTreeService
{
    public Task StageAsync(string repositoryPath, string filePath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var repo = new Repository(repositoryPath);
            if (repo.Info.IsBare) throw new InvalidOperationException("A bare repository has no working tree.");
            Commands.Stage(repo, filePath);
        }, ct);

    public Task UnstageAsync(string repositoryPath, string filePath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var repo = new Repository(repositoryPath);
            if (repo.Info.IsBare) throw new InvalidOperationException("A bare repository has no working tree.");
            Commands.Unstage(repo, filePath);
        }, ct);

    public Task<string> CommitAsync(string repositoryPath, string message, string authorName, string authorEmail, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("A commit message is required.", nameof(message));
            if (string.IsNullOrWhiteSpace(authorName) || string.IsNullOrWhiteSpace(authorEmail))
                throw new ArgumentException("A commit author name and email are required.");
            using var repo = new Repository(repositoryPath);
            if (repo.Info.IsBare) throw new InvalidOperationException("Cannot commit from a bare repository.");
            if (!repo.RetrieveStatus().IsDirty && repo.Index.Count == 0)
                throw new InvalidOperationException("There are no changes to commit.");
            var signature = new Signature(authorName.Trim(), authorEmail.Trim(), DateTimeOffset.Now);
            return repo.Commit(message.Trim(), signature, signature).Sha;
        }, ct);
}
