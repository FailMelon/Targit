using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>
/// Where the checked-out branch pushes and pulls: a remote and a branch on it. <see cref="Published"/> is false
/// when the branch doesn't exist on the remote yet (it will be created by the next push).
/// </summary>
public sealed record GitUpstream(string Remote, string Branch, bool Published, int Ahead, int Behind)
{
    public string Name => Remote + "/" + Branch;
}

/// <summary>Reads and changes the checked-out branch's upstream (its branch.&lt;name&gt;.remote and .merge).</summary>
public sealed class GitUpstreamService
{
    /// <summary>The checked-out branch's upstream, or null when none is set or HEAD is detached.</summary>
    public Task<GitUpstream?> ReadAsync(string repositoryPath, CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                using var repository = new Repository(repositoryPath);
                return Read(repository);
            },
            cancellationToken
        );

    /// <summary>
    /// Makes the checked-out branch push to and pull from <paramref name="branch"/> on <paramref name="remote"/>.
    /// The branch doesn't have to exist on the remote yet; the next push creates it.
    /// </summary>
    public Task SetAsync(
        string repositoryPath,
        string remote,
        string branch,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                ArgumentException.ThrowIfNullOrWhiteSpace(remote);
                branch = branch.Trim();
                using var repository = new Repository(repositoryPath);
                if (repository.Info.IsBare || repository.Info.IsHeadDetached || repository.Head.Tip is null)
                {
                    throw new InvalidOperationException("Check out a branch with a commit first.");
                }

                if (repository.Network.Remotes[remote] is null)
                {
                    throw new InvalidOperationException($"Remote '{remote}' does not exist.");
                }

                if (branch.Length == 0 || !Reference.IsValidName("refs/heads/" + branch))
                {
                    throw new InvalidOperationException($"'{branch}' isn't a valid branch name.");
                }

                repository.Branches.Update(
                    repository.Head,
                    updater =>
                    {
                        updater.Remote = remote;
                        updater.UpstreamBranch = "refs/heads/" + branch;
                    }
                );
            },
            cancellationToken
        );

    /// <summary>The remote to use when a branch has no upstream: origin, or the only remote.</summary>
    public static string? DefaultRemote(IReadOnlyCollection<string> remotes) =>
        remotes.Contains("origin") ? "origin"
        : remotes.Count == 1 ? remotes.First()
        : null;

    internal static GitUpstream? Read(Repository repository)
    {
        if (repository.Info.IsBare || repository.Info.IsHeadDetached || repository.Head.Tip is null)
        {
            return null;
        }

        var name = repository.Head.FriendlyName;
        var remote = repository.Config.Get<string>($"branch.{name}.remote")?.Value;
        var merge = repository.Config.Get<string>($"branch.{name}.merge")?.Value;
        if (string.IsNullOrEmpty(remote) || string.IsNullOrEmpty(merge) || remote == ".")
        {
            return null;
        }

        var branch = merge.StartsWith("refs/heads/", StringComparison.Ordinal) ? merge["refs/heads/".Length..] : merge;
        var tracking = repository.Branches[$"refs/remotes/{remote}/{branch}"];
        if (tracking?.Tip is null)
        {
            return new GitUpstream(remote, branch, false, 0, 0);
        }

        var divergence = repository.ObjectDatabase.CalculateHistoryDivergence(repository.Head.Tip, tracking.Tip);
        return new GitUpstream(remote, branch, true, divergence.AheadBy ?? 0, divergence.BehindBy ?? 0);
    }
}
