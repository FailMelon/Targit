using LibGit2Sharp;

namespace GitLlama.Git;

/// <summary>Adds patterns to the repository's top-level .gitignore.</summary>
public sealed class GitIgnoreService
{
    /// <summary>
    /// Appends a pattern to .gitignore, creating the file if needed. Returns false when the pattern is already
    /// listed.
    /// </summary>
    public Task<bool> AddPatternAsync(
        string repositoryPath,
        string pattern,
        CancellationToken cancellationToken = default
    ) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
                pattern = pattern.Trim();
                if (pattern.Length == 0 || pattern.Contains('\n') || pattern.Contains('\r'))
                {
                    throw new ArgumentException("Enter a single ignore pattern.", nameof(pattern));
                }

                using var repository = new Repository(repositoryPath);
                if (repository.Info.IsBare)
                {
                    throw new InvalidOperationException("A bare repository has no working tree to ignore files in.");
                }

                var file = GitPathGuard.RegularWorkingFile(repository, ".gitignore");
                var existing = File.Exists(file) ? File.ReadAllText(file) : "";
                if (existing.Replace("\r\n", "\n").Split('\n').Any(line => line.Trim() == pattern))
                {
                    return false;
                }

                var newline = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
                var separator = existing.Length == 0 || existing.EndsWith('\n') ? "" : newline;
                File.AppendAllText(file, separator + pattern + newline);
                return true;
            },
            cancellationToken
        );

    /// <summary>Patterns offered for a file: the file itself, its extension and its folder.</summary>
    public static IReadOnlyList<(string Label, string Pattern)> Suggestions(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').Trim('/');
        var suggestions = new List<(string, string)> { ("Ignore this file", "/" + path) };
        var name = path[(path.LastIndexOf('/') + 1)..];
        var extension = Path.GetExtension(name);
        if (extension.Length > 1 && extension.Length < name.Length)
        {
            suggestions.Add(($"Ignore all {extension} files", "*" + extension));
        }

        var folder = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
        if (folder.Length > 0)
        {
            suggestions.Add(($"Ignore folder {folder}/", "/" + folder + "/"));
        }

        return suggestions;
    }
}
