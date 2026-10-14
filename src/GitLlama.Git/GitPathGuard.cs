using LibGit2Sharp;

namespace GitLlama.Git;

internal static class GitPathGuard
{
    public static string RegularWorkingFile(Repository repository, string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        if (
            string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Split('/').Any(part => part is "." or ".." or "")
        )
        {
            throw new ArgumentException("Choose a repository-relative file path.", nameof(relativePath));
        }

        var root = Path.GetFullPath(repository.Info.WorkingDirectory);
        var absolute = Path.GetFullPath(Path.Combine(root, path));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (
            !absolute.StartsWith(
                prefix,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
            )
        )
        {
            throw new ArgumentException("The file must stay inside the working directory.");
        }

        var current = root;
        foreach (var part in path.Split('/'))
        {
            current = Path.Combine(current, part);
            if (
                (File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0
            )
            {
                throw new InvalidOperationException(
                    "This operation does not follow symbolic links or linked directories."
                );
            }
        }

        return absolute;
    }
}
