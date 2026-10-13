using LibGit2Sharp;
namespace Targit.Git;
public sealed record GitCommitAncestryInfo(string Sha, string Subject, IReadOnlyList<string> Parents, bool IsMergeCommit);
public sealed class GitCommitAncestryService
{
    public Task<GitCommitAncestryInfo> InspectAsync(string repositoryPath, string commitSha, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
            cancellationToken.ThrowIfCancellationRequested();
            using var repo = new Repository(repositoryPath);
            var commit = repo.Lookup<Commit>(commitSha.Trim()) ?? throw new ArgumentException("Commit not found.", nameof(commitSha));
            cancellationToken.ThrowIfCancellationRequested();
            var parents = commit.Parents.Select(parent => parent.Sha).ToArray();
            return new GitCommitAncestryInfo(commit.Sha, commit.MessageShort, parents, parents.Length > 1);
        }, cancellationToken);
}