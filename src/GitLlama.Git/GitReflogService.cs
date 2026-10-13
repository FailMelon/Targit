using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitReflogInfo(string Sha, string PreviousSha, string Message, string Author, DateTimeOffset Date)
{
    public override string ToString() => $"{Sha[..7]}  {Message} · {Author} · {Date:g}";
}
public sealed class GitReflogService
{
    public Task<IReadOnlyList<GitReflogInfo>> ListAsync(string path, CancellationToken ct = default) => Task.Run<IReadOnlyList<GitReflogInfo>>(() =>
    {
        ct.ThrowIfCancellationRequested();
        using var repo = new Repository(path);
        if (repo.Head.Tip is null) return [];
        return repo.Refs.Log("HEAD").Take(500).Select(e => new GitReflogInfo(e.To.Sha, e.From.Sha, e.Message, e.Committer.Name, e.Committer.When)).ToArray();
    }, ct);
    public Task RecoverBranchAsync(string path, string sha, string name, CancellationToken ct = default) => Task.Run(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var repo = new Repository(path);
        if (repo.Branches[name] is not null) throw new InvalidOperationException("Choose a new branch name.");
        var commit = repo.Lookup<Commit>(sha) ?? throw new InvalidOperationException("This commit is no longer in the object database.");
        ct.ThrowIfCancellationRequested();
        repo.CreateBranch(name.Trim(), commit);
    }, ct);
}
