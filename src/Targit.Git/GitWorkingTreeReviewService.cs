using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitWorkingTreeReview(string Path, string StagedPatch, string UnstagedPatch,
    IReadOnlyList<string> UnstagedHunks);

/// <summary>Read-only working-tree review and guarded discard of unstaged tracked changes.</summary>
public sealed class GitWorkingTreeReviewService
{
    public async Task<GitWorkingTreeReview> InspectAsync(string repositoryPath, string path,
        CancellationToken cancellationToken = default)
    {
        var diff = await new GitDiffService().GetFileDiffAsync(repositoryPath, path, cancellationToken);
        var hunks = diff.UnstagedPatch.Split('\n')
            .Where(line => line.StartsWith("@@ ", StringComparison.Ordinal))
            .ToArray();
        return new GitWorkingTreeReview(path, diff.StagedPatch, diff.UnstagedPatch, hunks);
    }

    public static IReadOnlyList<GitDiffHunk> ParseHunks(string patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var result = new List<GitDiffHunk>();
        var lines = patch.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("@@ ", StringComparison.Ordinal)) continue;
            var header = lines[i];
            var body = new List<string>();
            for (i++; i < lines.Length && !lines[i].StartsWith("@@ ", StringComparison.Ordinal) &&
                 !lines[i].StartsWith("diff --git ", StringComparison.Ordinal); i++)
                body.Add(lines[i]);
            i--;
            result.Add(new GitDiffHunk(header, string.Join("\n", body)));
        }
        return result;
    }

    public static GitSideBySideDiff SideBySide(string patch)
    {
        var left = new List<string>();
        var right = new List<string>();
        foreach (var hunk in ParseHunks(patch))
        {
            left.Add(hunk.Header);
            right.Add(hunk.Header);
            foreach (var line in hunk.Body.Split('\n'))
            {
                if (line.Length == 0) continue;
                if (line[0] == '-') { left.Add(line); right.Add(""); }
                else if (line[0] == '+') { left.Add(""); right.Add(line); }
                else { left.Add(line); right.Add(line); }
            }
        }
        return new GitSideBySideDiff(string.Join("\n", left), string.Join("\n", right));
    }

    public Task DiscardUnstagedAsync(string repositoryPath, string path, string confirmation,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!string.Equals(confirmation, "DISCARD", StringComparison.Ordinal))
            throw new InvalidOperationException("Type DISCARD to confirm the irreversible working-tree change.");
        cancellationToken.ThrowIfCancellationRequested();
        using var repo = new Repository(repositoryPath);
        if (repo.Info.IsBare) throw new InvalidOperationException("A bare repository has no working tree.");
        var normalized = path.Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(segment => segment is ".." or "."))
            throw new ArgumentException("Expected a repository-relative file path.", nameof(path));
        var status = repo.RetrieveStatus(normalized);
        if ((status & FileStatus.NewInWorkdir) != 0)
            throw new InvalidOperationException("Untracked files are not discarded automatically.");
        if ((status & FileStatus.Conflicted) != 0)
            throw new InvalidOperationException("Resolve conflicts before discarding changes.");
        if ((status & (FileStatus.ModifiedInWorkdir | FileStatus.DeletedFromWorkdir |
                       FileStatus.TypeChangeInWorkdir | FileStatus.RenamedInWorkdir)) == 0)
            throw new InvalidOperationException("No unstaged tracked changes to discard.");
        cancellationToken.ThrowIfCancellationRequested();
        // The index is the source of truth: never restore from HEAD, which would erase staged edits.
        var entry = repo.Index[normalized];
        if (entry is null)
            throw new InvalidOperationException("The path is not present in the index; deletion recovery is not supported.");
        if (repo.Lookup<Blob>(entry.Id) is not { } blob)
            throw new InvalidOperationException("Unable to read staged file content.");
        var root = Path.GetFullPath(repo.Info.WorkingDirectory);
        var destination = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            throw new ArgumentException("Path escapes repository root.", nameof(path));
        if (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing to overwrite a symbolic link.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input = blob.GetContentStream();
        using var output = File.Create(destination);
        input.CopyTo(output);
    }, cancellationToken);
}

public sealed record GitDiffHunk(string Header, string Body);
public sealed record GitSideBySideDiff(string Before, string After);
