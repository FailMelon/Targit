using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitMergeBaseInfo(string FirstSha, string SecondSha, string? BaseSha, string? BaseSubject);

public sealed class GitMergeBaseInspectionService
{
    public Task<GitMergeBaseInfo> InspectAsync(string repositoryPath, string firstSha, string secondSha,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(firstSha);
            ArgumentException.ThrowIfNullOrWhiteSpace(secondSha);
            cancellationToken.ThrowIfCancellationRequested();
            using var repository = new Repository(repositoryPath);
            var first = repository.Lookup<Commit>(firstSha.Trim())
                ?? throw new ArgumentException("First commit not found.", nameof(firstSha));
            var second = repository.Lookup<Commit>(secondSha.Trim())
                ?? throw new ArgumentException("Second commit not found.", nameof(secondSha));
            cancellationToken.ThrowIfCancellationRequested();
            var common = repository.ObjectDatabase.FindMergeBase(first, second);
            cancellationToken.ThrowIfCancellationRequested();
            return new GitMergeBaseInfo(first.Sha, second.Sha, common?.Sha, common?.MessageShort);
        }, cancellationToken);
}