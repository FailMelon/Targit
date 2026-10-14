using LibGit2Sharp;

namespace GitLlama.Git;

public sealed class GitRuntimeService
{
    public static string Describe()
    {
        var version = GlobalSettings.Version;
        return $"LibGit2Sharp: {typeof(Repository).Assembly.GetName().Version}\nNative Git: {version.InformationalVersion}\nNative features: {version.Features}\nNative libgit2 revision: {version.LibGit2CommitSha}";
    }

    public Task<IReadOnlyList<string>> WatchRootsAsync(string path) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            using var repo = new Repository(path);
            return repo.Info.IsBare ? [repo.Info.Path] : [repo.Info.WorkingDirectory, repo.Info.Path];
        });
}
