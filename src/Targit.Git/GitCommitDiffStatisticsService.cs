using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitCommitDiffStatistics(
    string Sha, string Subject, int FilesChanged, int LinesAdded, int LinesDeleted, bool IsRoot);

public sealed class GitCommitDiffStatisticsService
{
    public Task<GitCommitDiffStatistics> InspectAsync(string repositoryPath, string commitSha,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
            cancellationToken.ThrowIfCancellationRequested();
            using var repository = new Repository(repositoryPath);
            var commit = repository.Lookup<Commit>(commitSha.Trim())
                ?? throw new ArgumentException("Commit not found.", nameof(commitSha));
            cancellationToken.ThrowIfCancellationRequested();
            var parent = commit.Parents.FirstOrDefault();
            var patch = repository.Diff.Compare<Patch>(parent?.Tree, commit.Tree);
            cancellationToken.ThrowIfCancellationRequested();
            return new GitCommitDiffStatistics(commit.Sha, commit.MessageShort,
                patch.Count(), patch.LinesAdded, patch.LinesDeleted, parent is null);
        }, cancellationToken);
}