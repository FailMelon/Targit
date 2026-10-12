using LibGit2Sharp;
namespace Targit.Git;
public sealed record GitTagInfo(string Name, string TargetSha, bool IsAnnotated);
public sealed class GitTagInspectionService
{
    public Task<IReadOnlyList<GitTagInfo>> GetAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitTagInfo>>(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repo = new Repository(repositoryPath);
        var result = new List<GitTagInfo>();
        foreach (var tag in repo.Tags.OrderBy(t => t.FriendlyName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tag.PeeledTarget is not Commit commit) continue;
            result.Add(new GitTagInfo(tag.FriendlyName, commit.Sha, tag.Target is TagAnnotation));
        }
        return result;
    }, cancellationToken);
}