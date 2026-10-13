using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitCommitDescendant(string Sha, string Subject, int Distance);

public sealed class GitCommitDescendantService
{
    public Task<IReadOnlyList<GitCommitDescendant>> GetAsync(
        string repositoryPath, string ancestorSha, int maxCount = 100,
        CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<GitCommitDescendant>>(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(ancestorSha);
            if (maxCount is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maxCount));
            cancellationToken.ThrowIfCancellationRequested();
            using var repository = new Repository(repositoryPath);
            var ancestor = repository.Lookup<Commit>(ancestorSha.Trim())
                ?? throw new ArgumentException("Commit not found.", nameof(ancestorSha));

            // Walk reachable history once and propagate the shortest parent-to-child distance.
            // Commits that do not descend from the selected ancestor are excluded.
            var commits = repository.Commits.ToArray();
            var children = new Dictionary<string, List<Commit>>(StringComparer.Ordinal);
            foreach (var commit in commits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var parent in commit.Parents)
                {
                    if (!children.TryGetValue(parent.Sha, out var list))
                        children[parent.Sha] = list = [];
                    list.Add(commit);
                }
            }

            var distances = new Dictionary<string, int>(StringComparer.Ordinal) { [ancestor.Sha] = 0 };
            var queue = new Queue<Commit>();
            queue.Enqueue(ancestor);
            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parent = queue.Dequeue();
                if (!children.TryGetValue(parent.Sha, out var descendants)) continue;
                foreach (var child in descendants)
                {
                    if (distances.ContainsKey(child.Sha)) continue;
                    distances[child.Sha] = distances[parent.Sha] + 1;
                    queue.Enqueue(child);
                }
            }

            return commits.Where(c => distances.TryGetValue(c.Sha, out var distance) && distance > 0)
                .OrderBy(c => distances[c.Sha]).ThenBy(c => c.Sha, StringComparer.Ordinal)
                .Take(maxCount)
                .Select(c => new GitCommitDescendant(c.Sha, c.MessageShort, distances[c.Sha]))
                .ToArray();
        }, cancellationToken);
}