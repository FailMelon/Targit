using LibGit2Sharp;
using Targit.Core;
namespace Targit.Git;
public sealed class LibGitOverviewReader : IRepositoryOverviewReader
{
    public Task<RepositoryOverview> ReadOverviewAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var repo = new Repository(path);
            var commits = repo.Commits.Take(100)
                .Select(c => new CommitSummary(c.Sha, c.MessageShort, c.Author.Name, c.Author.When)).ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            var changes = repo.RetrieveStatus().Select(e => new ChangedFile(e.FilePath, e.State.ToString())).ToArray();
            var branches = repo.Branches.Select(b => b.FriendlyName).OrderBy(b => b).ToArray();
            return new RepositoryOverview(commits, changes, branches);
        }, cancellationToken);
}