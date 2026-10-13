using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitCommitFilePatch(string CommitSha, string Path, string PatchText, int LinesAdded, int LinesDeleted);

public sealed class GitCommitFilePatchService
{
    public Task<GitCommitFilePatch> GetAsync(string repositoryPath, string commitSha, string filePath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            cancellationToken.ThrowIfCancellationRequested();
            using var repository = new Repository(repositoryPath);
            var commit = repository.Lookup<Commit>(commitSha.Trim())
                ?? throw new ArgumentException("Commit not found.", nameof(commitSha));
            cancellationToken.ThrowIfCancellationRequested();
            var parent = commit.Parents.FirstOrDefault();
            var patch = repository.Diff.Compare<Patch>(parent?.Tree, commit.Tree);
            cancellationToken.ThrowIfCancellationRequested();
            var entry = patch.FirstOrDefault(change => string.Equals(change.Path, filePath.Trim(), StringComparison.Ordinal))
                ?? throw new ArgumentException("File has no patch in this commit.", nameof(filePath));
            return new GitCommitFilePatch(commit.Sha, entry.Path, entry.Patch, entry.LinesAdded, entry.LinesDeleted);
        }, cancellationToken);
}