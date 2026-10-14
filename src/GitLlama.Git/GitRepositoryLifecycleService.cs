using LibGit2Sharp;

namespace GitLlama.Git;

public sealed class GitRepositoryLifecycleService
{
    public Task<string> InitializeAsync(string path, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var target = ValidateDestination(path, allowExistingFiles: true);
        cancellationToken.ThrowIfCancellationRequested();
        Repository.Init(target);
        using var repository = new Repository(target);
        // A predictable initial branch on all platforms.
        repository.Refs.UpdateTarget("HEAD", "refs/heads/main");
        return target;
    }, cancellationToken);

    public Task<string> CloneAsync(string url, string path, GitNetworkOptions? network = null,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        network?.EnsureAzureAccess(url);
        var target = ValidateDestination(path);
        cancellationToken.ThrowIfCancellationRequested();
        if (GitSystemTransport.IsSsh(url) || network?.UsesAzureAuthorization(url) == true)
        {
            GitSystemTransport.RunAsync(null, ["clone", "--", url.Trim(), target], network ?? new GitNetworkOptions(), cancellationToken, ssh: GitSystemTransport.IsSsh(url), sshRemoteUrl: url).GetAwaiter().GetResult();
            return target;
        }
        var options = new CloneOptions
        {
            OnCheckoutProgress = (_, completed, total) => network?.Progress?.Report(new("Checking out files", completed, total))
        };
        var fetch = (network ?? new GitNetworkOptions()).Fetch(cancellationToken);
        options.FetchOptions.CredentialsProvider = fetch.CredentialsProvider;
        options.FetchOptions.OnTransferProgress = fetch.OnTransferProgress;
        Repository.Clone(url.Trim(), target, options);
        cancellationToken.ThrowIfCancellationRequested();
        return target;
    }, cancellationToken);

    private static string ValidateDestination(string path, bool allowExistingFiles = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var target = Path.GetFullPath(path.Trim());
        if (allowExistingFiles && (File.Exists(Path.Combine(target, ".git")) || Directory.Exists(Path.Combine(target, ".git"))))
            throw new InvalidOperationException("This folder is already a repository. Open it instead.");
        if (File.Exists(target) || (!allowExistingFiles && Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()))
            throw new InvalidOperationException("Choose a new or empty destination directory.");
        return target;
    }
}
