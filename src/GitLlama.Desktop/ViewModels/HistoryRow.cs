using GitLlama.Git;

namespace GitLlama.Desktop.ViewModels;

public sealed record GraphSegment(int FromLane, double FromY, int ToLane, double ToY, int ColorLane);

public sealed record HistoryRow(GitCommitNode Commit, int Lane, double GraphWidth, IReadOnlyList<GraphSegment> Segments)
{
    public string Sha => Commit.Sha;
    public string ShortSha => Sha[..Math.Min(7, Sha.Length)];
    public string Subject => Commit.Subject;
    public string Author => Commit.Author;
    public string Date => Commit.Timestamp.LocalDateTime.ToString("MMM d, HH:mm");
    public string FullDate => Commit.Timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz");
    public IReadOnlyList<string> Labels => Commit.Labels;

    // Keep lanes stable until their pending parent is reached. Vacant lanes are reused.
    public static IReadOnlyList<HistoryRow> Layout(IReadOnlyList<GitCommitNode> commits)
    {
        var pending = new List<string?>();
        var lanes = new int[commits.Count];
        var index = commits.Select((c, i) => (c.Sha, i)).ToDictionary(c => c.Sha, c => c.i);
        for (var i = 0; i < commits.Count; i++)
        {
            var commit = commits[i];
            var lane = pending.IndexOf(commit.Sha);
            if (lane < 0)
            {
                lane = pending.IndexOf(null);
                if (lane < 0) { lane = pending.Count; pending.Add(null); }
            }
            lanes[i] = lane;
            pending[lane] = null;
            foreach (var parent in commit.Parents)
            {
                if (pending.Contains(parent)) continue;
                var free = pending.IndexOf(null);
                if (free < 0) { free = pending.Count; pending.Add(null); }
                pending[free] = parent;
            }
        }
        var segments = Enumerable.Range(0, commits.Count).Select(_ => new List<GraphSegment>()).ToArray();
        for (var i = 0; i < commits.Count; i++)
        {
            foreach (var parent in commits[i].Parents)
            {
                if (!index.TryGetValue(parent, out var end) || end <= i)
                {
                    segments[i].Add(new(lanes[i], .5, lanes[i], 1, lanes[i]));
                    continue;
                }
                // Additional parents split immediately; first parents merge at their destination.
                var track = parent == commits[i].Parents[0] ? lanes[i] : lanes[end];
                var color = parent == commits[i].Parents[0] ? lanes[i] : lanes[end];
                segments[i].Add(new(lanes[i], .5, track, 1, color));
                for (var j = i + 1; j < end; j++) segments[j].Add(new(track, 0, track, 1, color));
                segments[end].Add(new(track, 0, lanes[end], .5, color));
            }
        }
        var width = Math.Max(84, (lanes.DefaultIfEmpty(0).Max() + 1) * 18 + 24);
        return commits.Select((c, i) => new HistoryRow(c, lanes[i], width, segments[i])).ToArray();
    }
}
