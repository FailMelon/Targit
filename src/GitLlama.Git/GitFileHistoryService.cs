using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitFileHistoryEntry(string Sha, string Subject);

public sealed class GitFileHistoryService
{
    public Task<IReadOnlyList<GitFileHistoryEntry>> GetAsync(
        string repositoryPath,
        string relativePath,
        int maxCount = 100,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run<IReadOnlyList<GitFileHistoryEntry>>(
            () =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
                if (Path.IsPathRooted(relativePath) || relativePath.Split('/', '\\').Contains(".."))
                {
                    throw new ArgumentException("A repository-relative path is required.", nameof(relativePath));
                }

                if (maxCount is < 1 or > 1000)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxCount));
                }

                using var repo = new Repository(repositoryPath);
                var path = relativePath.Replace('\\', '/');
                var result = new List<GitFileHistoryEntry>();
                foreach (var commit in repo.Commits)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (
                        commit.Parents.Any()
                        && !commit.Parents.Any(parent =>
                            repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree)
                                .Any(change => change.Path == path || change.OldPath == path)
                        )
                    )
                    {
                        continue;
                    }

                    if (!commit.Parents.Any() && commit.Tree[path] is null)
                    {
                        continue;
                    }

                    result.Add(new GitFileHistoryEntry(commit.Sha, commit.MessageShort));
                    if (result.Count == maxCount)
                    {
                        break;
                    }
                }

                return result;
            },
            cancellationToken
        );
}
