using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitHistoryFile(string Path, string Status, int Added, int Deleted);
public sealed record GitHistoryCommit(string Sha, string Subject, string Message, string Author,
    DateTimeOffset Date, IReadOnlyList<string> Parents, IReadOnlyList<string> Labels,
    IReadOnlyList<GitHistoryFile> Files);

/// <summary>Single read-only snapshot for the unified History workspace.</summary>
public sealed class GitHistoryWorkspaceService
{
    public Task<GitHistoryCommit> InspectAsync(string repositoryPath, string sha,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        cancellationToken.ThrowIfCancellationRequested();
        using var repo = new Repository(repositoryPath);
        var commit = repo.Lookup<Commit>(sha.Trim())
            ?? throw new ArgumentException("Commit not found.", nameof(sha));
        var labels = GitDisplayReferences.Labels(repo).Where(reference => reference.Sha == commit.Sha).Select(reference => reference.Name)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var patch = repo.Diff.Compare<Patch>(commit.Parents.FirstOrDefault()?.Tree, commit.Tree);
        var files = patch.Select(p =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new GitHistoryFile(p.Path, p.Status.ToString(), p.LinesAdded, p.LinesDeleted);
        }).OrderBy(p => p.Path, StringComparer.Ordinal).ToArray();
        return new GitHistoryCommit(commit.Sha, commit.MessageShort, commit.Message,
            commit.Author.Name, commit.Author.When, commit.Parents.Select(p => p.Sha).ToArray(),
            labels, files);
    }, cancellationToken);
}
