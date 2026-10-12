using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class WorkingTreeTests
{
    [Fact]
    public async Task StageUnstageAndCommitUseLibGit2()
    {
        var directory = Path.Combine(Path.GetTempPath(), "targit-phase2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Repository.Init(directory);
            File.WriteAllText(Path.Combine(directory, "sample.txt"), "hello");
            var service = new GitWorkingTreeService();

            await service.StageAsync(directory, "sample.txt");
            using (var repo = new Repository(directory))
                Assert.True(repo.RetrieveStatus("sample.txt").HasFlag(FileStatus.NewInIndex));

            await service.UnstageAsync(directory, "sample.txt");
            using (var repo = new Repository(directory))
                Assert.True(repo.RetrieveStatus("sample.txt").HasFlag(FileStatus.NewInWorkdir));

            await service.StageAsync(directory, "sample.txt");
            var sha = await service.CommitAsync(directory, "Add sample", "Targit Tests", "tests@example.invalid");
            using (var repo = new Repository(directory))
            {
                Assert.Equal(sha, repo.Head.Tip.Sha);
                Assert.Equal("Add sample", repo.Head.Tip.MessageShort);
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task UnstagedFilesCannotBeCommitted()
    {
        var directory = Path.Combine(Path.GetTempPath(), "targit-unstaged-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Repository.Init(directory);
            File.WriteAllText(Path.Combine(directory, "unstaged.txt"), "content");
            var service = new GitWorkingTreeService();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CommitAsync(directory, "Should fail", "Tester", "test@example.invalid"));
            Assert.Contains("Stage", error.Message);
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task SelectiveCommitLeavesUnstagedFileUntouched()
    {
        var directory = Path.Combine(Path.GetTempPath(), "targit-selective-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Repository.Init(directory);
            File.WriteAllText(Path.Combine(directory, "staged.txt"), "yes");
            File.WriteAllText(Path.Combine(directory, "unstaged.txt"), "no");
            var service = new GitWorkingTreeService();
            await service.StageAsync(directory, "staged.txt");
            await service.CommitAsync(directory, "Selective commit", "Tester", "test@example.invalid");
            using var repo = new Repository(directory);
            Assert.NotNull(repo.Head.Tip.Tree["staged.txt"]);
            Assert.Null(repo.Head.Tip.Tree["unstaged.txt"]);
            Assert.True(repo.RetrieveStatus().IsDirty);
        }
        finally { Cleanup(directory); }
    }

    private static void Cleanup(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        Directory.Delete(directory, true);
    }

    [Fact]
    public async Task CommitRequiresMessageAndAuthor()
    {
        var service = new GitWorkingTreeService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CommitAsync(".", "", "Tester", "test@example.invalid"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CommitAsync(".", "message", "", "test@example.invalid"));
    }
}
