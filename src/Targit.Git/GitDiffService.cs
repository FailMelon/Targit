using LibGit2Sharp;

namespace Targit.Git;

/// <summary>Reads staged and unstaged patches without modifying the repository.</summary>
public sealed class GitDiffService
{
    public Task<GitFileDiff> GetFileDiffAsync(
        string repositoryPath, string relativePath, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
            using var repository = new Repository(repositoryPath);
            if (repository.Info.IsBare)
                throw new InvalidOperationException("A bare repository has no working tree.");

            var paths = new[] { relativePath };
            var options = new CompareOptions { ContextLines = 3 };
            var headTree = repository.Head.Tip?.Tree;
            cancellationToken.ThrowIfCancellationRequested();

            using var staged = repository.Diff.Compare<Patch>(
                headTree, DiffTargets.Index, paths, null, options);
            using var unstaged = repository.Diff.Compare<Patch>(DiffTargets.Index | DiffTargets.WorkingDirectory, paths, null, options);

            cancellationToken.ThrowIfCancellationRequested();
            var unstagedContent = unstaged.Content;
            // Index-to-working-directory comparisons omit untracked files.
            if ((repository.RetrieveStatus(relativePath) & FileStatus.NewInWorkdir) != 0)
            {
                var root = Path.GetFullPath(repository.Info.WorkingDirectory);
                var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
                if (!fullPath.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new ArgumentException("The selected path must be inside the repository.", nameof(relativePath));
                if (File.Exists(fullPath))
                {
                    var lines = File.ReadAllLines(fullPath);
                    unstagedContent = $"--- /dev/null\n+++ b/{relativePath}\n@@ -0,0 +1,{lines.Length} @@\n" +
                                      string.Concat(lines.Select(line => "+" + line + "\n"));
                }
            }
            return new GitFileDiff(relativePath, staged.Content, unstagedContent);
        }, cancellationToken);
}

public sealed record GitFileDiff(string Path, string StagedPatch, string UnstagedPatch);
