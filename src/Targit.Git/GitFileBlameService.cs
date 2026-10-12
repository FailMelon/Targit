using LibGit2Sharp;
namespace Targit.Git;
public sealed record GitBlameLine(int StartLine, int LineCount, string CommitSha, string Author);
public sealed class GitFileBlameService
{
    public Task<IReadOnlyList<GitBlameLine>> GetAsync(string repositoryPath, string relativePath, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<GitBlameLine>>(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath) || relativePath.Split('/', '\\').Contains(".."))
            throw new ArgumentException("A repository-relative path is required.", nameof(relativePath));
        using var repo = new Repository(repositoryPath);
        var result = new List<GitBlameLine>();
        foreach (var hunk in repo.Blame(relativePath.Replace('\\', '/')))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(new GitBlameLine(hunk.FinalStartLineNumber, hunk.LineCount, hunk.FinalCommit.Sha, hunk.FinalSignature.Name));
        }
        return result;
    }, cancellationToken);
}