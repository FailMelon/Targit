using LibGit2Sharp;

namespace GitLlama.Git;

public enum GitCommitRelationship
{
    Same,
    FirstIsAncestor,
    SecondIsAncestor,
    Diverged,
    Unrelated
}

public sealed record GitCommitRelationshipInfo(
    string FirstSha, string SecondSha, GitCommitRelationship Relationship, string? MergeBaseSha);

public sealed class GitCommitRelationshipService
{
    public Task<GitCommitRelationshipInfo> InspectAsync(string repositoryPath, string firstSha, string secondSha,
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
            var mergeBase = repository.ObjectDatabase.FindMergeBase(first, second);
            cancellationToken.ThrowIfCancellationRequested();
            var relationship = first.Sha == second.Sha ? GitCommitRelationship.Same
                : mergeBase is null ? GitCommitRelationship.Unrelated
                : mergeBase.Sha == first.Sha ? GitCommitRelationship.FirstIsAncestor
                : mergeBase.Sha == second.Sha ? GitCommitRelationship.SecondIsAncestor
                : GitCommitRelationship.Diverged;
            return new GitCommitRelationshipInfo(first.Sha, second.Sha, relationship, mergeBase?.Sha);
        }, cancellationToken);
}