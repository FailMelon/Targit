using LibGit2Sharp;

namespace GitLlama.Git;

public enum RemoteBranchOperation
{
    Create,
    Rename,
    Delete,
}

public sealed record RemoteBranchDraft(
    string RepositoryPath,
    string RemoteName,
    string RemoteUrl,
    string? BranchName,
    string CommitSha,
    string SourceName
);

public sealed partial class GitRemoteService
{
    public Task<RemoteBranchDraft> PrepareBranchAsync(
        string path,
        string remoteName,
        string? branchName,
        CancellationToken ct = default
    ) =>
        Task.Run(
            () =>
            {
                ct.ThrowIfCancellationRequested();
                using var repo = new Repository(path);
                var remote =
                    repo.Network.Remotes[remoteName]
                    ?? throw new InvalidOperationException("The remote no longer exists.");
                if (branchName == "HEAD")
                {
                    throw new InvalidOperationException("Select a branch rather than the remote HEAD alias.");
                }

                var branch = branchName is null ? repo.Head : repo.Branches[$"refs/remotes/{remoteName}/{branchName}"];
                if (branch?.Tip is null || (branchName is null && repo.Info.IsHeadDetached))
                {
                    throw new InvalidOperationException(
                        "Select a branch with a commit. Fetch the remote first if needed."
                    );
                }

                return new RemoteBranchDraft(
                    path,
                    remoteName,
                    remote.PushUrl ?? remote.Url,
                    branchName,
                    branch.Tip.Sha,
                    branchName is null ? branch.FriendlyName : remoteName + "/" + branchName
                );
            },
            ct
        );

    public async Task ApplyBranchAsync(
        RemoteBranchDraft draft,
        RemoteBranchOperation operation,
        string newName,
        GitNetworkOptions options,
        CancellationToken ct = default
    )
    {
        newName = newName.Trim();
        if (
            operation != RemoteBranchOperation.Delete
            && (!GitBranchService.BranchNameIsValid(newName) || newName == "HEAD")
        )
        {
            throw new InvalidOperationException("Enter a valid branch name.");
        }

        if (
            operation != RemoteBranchOperation.Create
            && (draft.BranchName is null || !GitBranchService.BranchNameIsValid(draft.BranchName))
        )
        {
            throw new InvalidOperationException("Select an existing remote branch.");
        }

        if (operation == RemoteBranchOperation.Rename && newName == draft.BranchName)
        {
            throw new InvalidOperationException("Choose a different branch name.");
        }

        using (var repo = new Repository(draft.RepositoryPath))
        {
            var remote = repo.Network.Remotes[draft.RemoteName];
            if (remote is null || (remote.PushUrl ?? remote.Url) != draft.RemoteUrl)
            {
                throw new InvalidOperationException(
                    "The remote changed. Reopen the action and review its destination."
                );
            }

            if (repo.Lookup<Commit>(draft.CommitSha) is null)
            {
                throw new InvalidOperationException("The source commit is no longer available. Fetch and try again.");
            }
        }

        options.EnsureAzureAccess(draft.RemoteUrl);
        var advertised = await GitSystemTransport
            .RunAsync(
                draft.RepositoryPath,
                ["ls-remote", "--heads", "--", draft.RemoteName],
                options,
                ct,
                ssh: GitSystemTransport.IsSsh(draft.RemoteUrl),
                sshRemoteUrl: draft.RemoteUrl,
                httpsRemoteUrl: draft.RemoteUrl
            )
            .ConfigureAwait(false);
        var heads = advertised
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r').Split('\t'))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[1], parts => parts[0], StringComparer.Ordinal);
        if (operation != RemoteBranchOperation.Delete && heads.ContainsKey("refs/heads/" + newName))
        {
            throw new InvalidOperationException("The destination branch already exists. Choose a different name.");
        }

        if (
            operation != RemoteBranchOperation.Create
            && heads.GetValueOrDefault("refs/heads/" + draft.BranchName) != draft.CommitSha
        )
        {
            throw new InvalidOperationException("The branch changed on the server. Fetch and reopen the action.");
        }

        var arguments = new List<string> { "push", "--atomic" };
        if (operation != RemoteBranchOperation.Delete)
        {
            arguments.Add($"--force-with-lease=refs/heads/{newName}:"); // Require a new, absent destination.
        }

        if (operation != RemoteBranchOperation.Create)
        {
            arguments.Add($"--force-with-lease=refs/heads/{draft.BranchName}:{draft.CommitSha}");
        }

        arguments.Add("--");
        arguments.Add(draft.RemoteName);
        if (operation != RemoteBranchOperation.Delete)
        {
            arguments.Add($"{draft.CommitSha}:refs/heads/{newName}");
        }

        if (operation != RemoteBranchOperation.Create)
        {
            arguments.Add($":refs/heads/{draft.BranchName}");
        }

        await GitSystemTransport
            .RunAsync(
                draft.RepositoryPath,
                arguments,
                options,
                ct,
                ssh: GitSystemTransport.IsSsh(draft.RemoteUrl),
                sshRemoteUrl: draft.RemoteUrl,
                httpsRemoteUrl: draft.RemoteUrl
            )
            .ConfigureAwait(false);
        // Reflect the confirmed server transaction locally without changing local branch names.
        using var updated = new Repository(draft.RepositoryPath);
        if (operation != RemoteBranchOperation.Delete)
        {
            updated.Refs.Add($"refs/remotes/{draft.RemoteName}/{newName}", draft.CommitSha, allowOverwrite: true);
        }

        if (operation != RemoteBranchOperation.Create)
        {
            var oldRef = $"refs/remotes/{draft.RemoteName}/{draft.BranchName}";
            if (operation == RemoteBranchOperation.Rename)
            {
                foreach (
                    var local in updated
                        .Branches.Where(branch => !branch.IsRemote && branch.TrackedBranch?.CanonicalName == oldRef)
                        .ToArray()
                )
                {
                    updated.Branches.Update(
                        local,
                        updater => updater.TrackedBranch = $"refs/remotes/{draft.RemoteName}/{newName}"
                    );
                }
            }

            if (updated.Refs[oldRef] is not null)
            {
                updated.Refs.Remove(oldRef);
            }
        }
    }
}
