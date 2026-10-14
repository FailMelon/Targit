using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitWorktreeInfo(string Name, string Path, string Branch, bool IsLocked, string LockReason)
{
    public override string ToString() => $"{Name} · {Branch}{(IsLocked ? " · locked" : "")} · {Path}";
}

public sealed class GitWorktreeService
{
    public Task<IReadOnlyList<GitWorktreeInfo>> ListAsync(string path, CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<GitWorktreeInfo>>(
            () =>
            {
                using var repo = new Repository(path);
                var result = new List<GitWorktreeInfo>();
                foreach (var worktree in repo.Worktrees)
                {
                    if (worktree is null)
                    {
                        continue;
                    }

                    ct.ThrowIfCancellationRequested();
                    using var linked = worktree.WorktreeRepository;
                    result.Add(
                        new(
                            worktree.Name,
                            linked.Info.WorkingDirectory,
                            linked.Head.FriendlyName,
                            worktree.IsLocked,
                            worktree.LockReason ?? ""
                        )
                    );
                }

                return result;
            },
            ct
        );

    public Task CreateAsync(
        string path,
        string name,
        string destination,
        string branch,
        CancellationToken ct = default
    ) =>
        Task.Run(
            () =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(name);
                ArgumentException.ThrowIfNullOrWhiteSpace(branch);
                if (
                    name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || name.Contains('/')
                    || name.Contains('\\')
                    || name is "." or ".."
                )
                {
                    throw new ArgumentException("Choose a valid worktree name.");
                }

                var target = Path.GetFullPath(destination);
                if (File.Exists(target) || Directory.Exists(target))
                {
                    throw new InvalidOperationException("Choose a new destination directory.");
                }

                using var repo = new Repository(path);
                if (repo.Head.Tip is null || repo.Info.IsBare)
                {
                    throw new InvalidOperationException("A working repository with a commit is required.");
                }

                if (repo.Worktrees[name] is not null)
                {
                    throw new InvalidOperationException("A worktree with that name exists.");
                }

                if (repo.Branches[name] is not null)
                {
                    throw new InvalidOperationException(
                        "Choose a worktree name different from an existing branch name."
                    );
                }

                var local =
                    repo.Branches[branch] ?? throw new InvalidOperationException("Choose an existing local branch.");
                if (local.IsRemote || local.FriendlyName == repo.Head.FriendlyName)
                {
                    throw new InvalidOperationException("Choose a local branch that is not checked out.");
                }

                foreach (var existing in repo.Worktrees)
                {
                    if (existing is null)
                    {
                        continue;
                    }

                    using var linked = existing.WorktreeRepository;
                    if (linked.Head.CanonicalName == local.CanonicalName)
                    {
                        throw new InvalidOperationException("This branch is already checked out in another worktree.");
                    }
                }

                ct.ThrowIfCancellationRequested();
                repo.Worktrees.Add(local.CanonicalName, name.Trim(), target, false);
                // LibGit2Sharp creates a temporary branch named after the worktree before checkout.
                // It is no longer checked out after Add switches to the requested branch.
                repo.Branches.Remove(name.Trim());
            },
            ct
        );

    public Task SetLockedAsync(string path, string name, bool locked, CancellationToken ct = default) =>
        Task.Run(
            () =>
            {
                using var repo = new Repository(path);
                var worktree = repo.Worktrees[name] ?? throw new InvalidOperationException("Worktree not found.");
                ct.ThrowIfCancellationRequested();
                if (locked)
                {
                    worktree.Lock("Locked in GitLlama");
                }
                else
                {
                    worktree.Unlock();
                }
            },
            ct
        );

    /// <summary>Remove registration while preserving the complete working folder as a sibling backup.</summary>
    public Task<string> RemoveAsync(string path, string name, string confirmation, CancellationToken ct = default) =>
        Task.Run(
            () =>
            {
                if (confirmation != name)
                {
                    throw new InvalidOperationException("Type the selected worktree name to confirm removal.");
                }

                using var repo = new Repository(path);
                var worktree = repo.Worktrees[name] ?? throw new InvalidOperationException("Worktree not found.");
                if (worktree.IsLocked)
                {
                    throw new InvalidOperationException("Unlock the worktree before removing it.");
                }

                string target;
                using (var linked = worktree.WorktreeRepository)
                {
                    if (linked.RetrieveStatus().IsDirty || linked.Info.CurrentOperation != CurrentOperation.None)
                    {
                        throw new InvalidOperationException(
                            "Commit or stash changes and finish any operation before removing this worktree."
                        );
                    }

                    target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(linked.Info.WorkingDirectory));
                }

                var primary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repo.Info.WorkingDirectory));
                if (
                    string.Equals(
                        target,
                        primary,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
                    )
                    || target == Path.GetPathRoot(target)
                    || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0
                )
                {
                    throw new InvalidOperationException("This worktree directory cannot be removed.");
                }

                var backup = target + ".gitllama-backup-" + Guid.NewGuid().ToString("N")[..8];
                ct.ThrowIfCancellationRequested();
                Directory.Move(target, backup);
                try
                {
                    if (!repo.Worktrees.Prune(worktree, false))
                    {
                        throw new InvalidOperationException("Native worktree removal did not complete.");
                    }
                }
                catch
                {
                    Directory.Move(backup, target);
                    throw;
                }

                return backup;
            },
            ct
        );
}
