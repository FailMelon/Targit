using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitSubmoduleInfo(string Name, string Path, string Url, string State, string Commit)
{
    public override string ToString() => $"{Path} · {State}";
}
public sealed class GitSubmoduleService
{
    public Task<IReadOnlyList<GitSubmoduleInfo>> ListAsync(string path, CancellationToken ct = default) => Task.Run<IReadOnlyList<GitSubmoduleInfo>>(() =>
    {
        using var repo = new Repository(path);
        return repo.Submodules.Select(module =>
        {
            ct.ThrowIfCancellationRequested();
            return new GitSubmoduleInfo(module.Name, module.Path, module.Url, module.RetrieveStatus().ToString(), module.WorkDirCommitId?.Sha ?? "Not initialized");
        }).ToArray();
    }, ct);
    public Task UpdateAsync(string path, string name, GitNetworkOptions? network = null, CancellationToken ct = default) => Task.Run(() =>
    {
        using var repo = new Repository(path);
        if (repo.Info.IsBare) throw new InvalidOperationException("Submodule checkout requires a working tree.");
        var module = repo.Submodules[name] ?? throw new InvalidOperationException("Submodule not found.");
        var directory = GitPathGuard.RegularWorkingFile(repo, module.Path);
        if (Repository.IsValid(directory))
        {
            using var child = new Repository(directory);
            if (child.RetrieveStatus().IsDirty || child.Info.CurrentOperation != CurrentOperation.None)
                throw new InvalidOperationException("Commit or stash changes inside the submodule before updating.");
        }
        var options = new SubmoduleUpdateOptions { Init = true };
        network?.EnsureAzureAccess(module.Url);
        if (GitSystemTransport.IsSsh(module.Url) || network?.UsesAzureAuthorization(module.Url) == true)
        {
            GitSystemTransport.RunAsync(path, ["submodule", "update", "--init", "--", module.Path], network ?? new GitNetworkOptions(), ct, ssh: GitSystemTransport.IsSsh(module.Url), sshRemoteUrl: module.Url).GetAwaiter().GetResult(); return;
        }
        var fetch = (network ?? new GitNetworkOptions()).Fetch(ct);
        options.FetchOptions.CredentialsProvider = fetch.CredentialsProvider;
        options.FetchOptions.OnTransferProgress = fetch.OnTransferProgress;
        ct.ThrowIfCancellationRequested();
        repo.Submodules.Update(name, options);
    }, ct);
}
