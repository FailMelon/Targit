using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitWorkingTreeServiceTests
{
    [Fact]
    public async Task StageCommitAndUnstageRoundTrip()
    {
        var path = NewRepository();
        try
        {
            var git = new GitWorkingTreeService();
            File.WriteAllText(Path.Combine(path, "one.txt"), "first");
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => git.CommitAsync(path, "No changes", "Test", "test@example.com"));

            await git.StageAsync(path, "one.txt");
            using (var repo = new Repository(path))
                Assert.Contains(repo.RetrieveStatus(), entry => entry.FilePath == "one.txt" &&
                    (entry.State & FileStatus.NewInIndex) != 0);

            await git.UnstageAsync(path, "one.txt");
            using (var repo = new Repository(path))
                Assert.Contains(repo.RetrieveStatus(), entry => entry.FilePath == "one.txt" &&
                    (entry.State & FileStatus.NewInWorkdir) != 0);

            await git.StageAsync(path, "one.txt");
            var sha = await git.CommitAsync(path, "Initial commit", "Test", "test@example.com");
            using (var repo = new Repository(path))
            {
                Assert.Equal(sha, repo.Head.Tip.Sha);
                Assert.Equal("Initial commit", repo.Head.Tip.MessageShort);
                Assert.Empty(repo.RetrieveStatus());
            }
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task OnlyStagedChangesAreCommitted()
    {
        var path = NewRepository();
        try
        {
            var git = new GitWorkingTreeService();
            File.WriteAllText(Path.Combine(path, "staged.txt"), "included");
            File.WriteAllText(Path.Combine(path, "unstaged.txt"), "excluded");
            await git.StageAsync(path, "staged.txt");
            await git.CommitAsync(path, "Selective commit", "Test", "test@example.com");
            using var repo = new Repository(path);
            Assert.NotNull(repo.Head.Tip["staged.txt"]);
            Assert.Null(repo.Head.Tip["unstaged.txt"]);
            Assert.Contains(repo.RetrieveStatus(), entry => entry.FilePath == "unstaged.txt");
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task CommitRequiresAuthorAndMessage()
    {
        var path = NewRepository();
        try
        {
            var git = new GitWorkingTreeService();
            await Assert.ThrowsAsync<ArgumentException>(() => git.CommitAsync(path, " ", "Test", "test@example.com"));
            await Assert.ThrowsAsync<ArgumentException>(() => git.CommitAsync(path, "Message", "", "test@example.com"));
            await Assert.ThrowsAsync<ArgumentException>(() => git.CommitAsync(path, "Message", "Test", ""));
        }
        finally { Cleanup(path); }
    }

    private static string NewRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Repository.Init(path);
        return path;
    }

    private static void Cleanup(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        Directory.Delete(path, true);
    }
}
