using LibGit2Sharp;
namespace Targit.Git;
public sealed record GitInspectedTag(string Name, string TargetSha, bool IsAnnotated);
public sealed class GitTagInspectionService
{
    public Task<IReadOnlyList<GitInspectedTag>> GetAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitInspectedTag>>(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        using var repo = new Repository(repositoryPath);
        var result = new List<GitInspectedTag>();
        foreach (var tag in repo.Tags.OrderBy(t => t.FriendlyName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tag.PeeledTarget is not Commit commit) continue;
            result.Add(new GitInspectedTag(tag.FriendlyName, commit.Sha, tag.Target is TagAnnotation));
        }
        return result;
    }, cancellationToken);
}