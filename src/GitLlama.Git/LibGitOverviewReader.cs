using LibGit2Sharp;
using GitLlama.Core;

namespace GitLlama.Git;

/// <summary>Reads a bounded repository overview using short-lived libgit2 handles.</summary>
public sealed class LibGitOverviewReader : IRepositoryOverviewReader
{
    public Task<RepositoryOverview> ReadOverviewAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var repo = new Repository(Path.GetFullPath(path));
            var commits = new List<CommitSummary>();
            foreach (var commit in repo.Commits.Take(100))
            {
                cancellationToken.ThrowIfCancellationRequested();
                commits.Add(new CommitSummary(commit.Sha, commit.MessageShort, commit.Author.Name, commit.Author.When));
            }

            // Bare repositories have no working tree to inspect.
            var changes = repo.Info.IsBare
                ? Array.Empty<ChangedFile>()
                : repo.RetrieveStatus(new StatusOptions { IncludeIgnored = false, RecurseUntrackedDirs = true })
                    .Where(entry => (entry.State & FileStatus.Ignored) == 0 && entry.State != FileStatus.Unaltered)
                    .Select(entry => new ChangedFile(entry.FilePath, entry.State.ToString())).ToArray();

            cancellationToken.ThrowIfCancellationRequested();
            var branches = repo.Branches
                .Select(branch => branch.FriendlyName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            return new RepositoryOverview(commits, changes, branches);
        }, cancellationToken);
}
