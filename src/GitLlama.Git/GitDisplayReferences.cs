using LibGit2Sharp;

namespace GitLlama.Git;

internal static class GitDisplayReferences
{
    public static IEnumerable<Branch> Branches(Repository repo)
    {
        var aliases = repo.Network.Remotes.Select(remote => $"refs/remotes/{remote.Name}/HEAD").ToHashSet(StringComparer.Ordinal);
        return repo.Branches.Where(branch => !aliases.Contains(branch.CanonicalName));
    }
    public static IEnumerable<(string Sha, string Name)> Labels(Repository repo)
    {
        foreach (var branch in Branches(repo)) if (branch.Tip is { } tip) yield return (tip.Sha, branch.FriendlyName);
        foreach (var tag in repo.Tags) if (tag.PeeledTarget is Commit commit) yield return (commit.Sha, "tag:" + tag.FriendlyName);
        if (repo.Info.IsHeadDetached && repo.Head.Tip is { } head) yield return (head.Sha, "HEAD");
    }
}
