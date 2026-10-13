using LibGit2Sharp;
namespace GitLlama.Git;
public sealed record GitGraphLane(string Sha, int Lane, IReadOnlyList<string> Parents);
public sealed class GitGraphLaneService
{
    public async Task<IReadOnlyList<GitGraphLane>> GetAsync(string repositoryPath, int maxCount = 200, CancellationToken cancellationToken = default)
    {
        var nodes = await new GitCommitGraphService().GetAsync(repositoryPath, maxCount, cancellationToken);
        var active = new List<string>();
        var result = new List<GitGraphLane>();
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lane = active.IndexOf(node.Sha);
            if (lane < 0) { lane = active.Count; active.Add(node.Sha); }
            active.RemoveAt(lane);
            for (var i = node.Parents.Count - 1; i >= 0; i--)
                if (!active.Contains(node.Parents[i], StringComparer.Ordinal))
                    active.Insert(Math.Min(lane, active.Count), node.Parents[i]);
            result.Add(new GitGraphLane(node.Sha, lane, node.Parents));
        }
        return result;
    }
}