using LibGit2Sharp;

namespace Targit.Git;

public enum GitConflictChoice { Ours, Theirs }

public sealed class GitConflictChoiceService
{
    public Task ResolveAsync(string repositoryPath, string relativePath, GitConflictChoice choice,
        CancellationToken ct = default) => Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        using var repo = new Repository(repositoryPath);
        if (repo.Info.IsBare) throw new InvalidOperationException("A working tree is required.");
        var conflict = repo.Index.Conflicts[relativePath]
            ?? throw new InvalidOperationException("The path is not conflicted.");
        var entry = choice == GitConflictChoice.Ours ? conflict.Ours : conflict.Theirs;
        if (entry is null) throw new InvalidOperationException("Selected stage is a deletion.");
        var root = Path.GetFullPath(repo.Info.WorkingDirectory);
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (Path.IsPathRooted(relativePath) || !full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Path escapes repository.", nameof(relativePath));
        if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Symbolic links are not supported.");
        var blob = repo.Lookup<Blob>(entry.Id) ?? throw new InvalidOperationException("Stage missing.");
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using (var input = blob.GetContentStream())
        using (var output = File.Create(full))
            input.CopyTo(output);
        Commands.Stage(repo, relativePath);
    }, ct);
}
