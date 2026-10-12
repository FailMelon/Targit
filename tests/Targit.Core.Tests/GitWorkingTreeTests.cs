using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitWorkingTreeTests
{
    [Fact]
    public async Task StageCommitAndUnstageAreIndexOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-phase2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitWorkingTreeService();
            File.WriteAllText(Path.Combine(path, "one.txt"), "first");
            File.WriteAllText(Path.Combine(path, "two.txt"), "not staged");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CommitAsync(path, "empty", "Test", "test@example.com"));
            await service.StageAsync(path, "one.txt");
            using (var repository = new Repository(path))
            {
                Assert.True((repository.RetrieveStatus("one.txt") & FileStatus.NewInIndex) != 0);
                Assert.True((repository.RetrieveStatus("two.txt") & FileStatus.NewInWorkdir) != 0);
            }
            await service.UnstageAsync(path, "one.txt");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CommitAsync(path, "still empty", "Test", "test@example.com"));
            await service.StageAsync(path, "one.txt");
            var sha = await service.CommitAsync(path, "First commit", "Test", "test@example.com");
            using var result = new Repository(path);
            Assert.Equal(sha, result.Head.Tip.Sha);
            Assert.Equal("First commit", result.Head.Tip.MessageShort);
            Assert.Equal("Test", result.Head.Tip.Author.Name);
            Assert.True((result.RetrieveStatus("two.txt") & FileStatus.NewInWorkdir) != 0);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task CommitRequiresValidInputs()
    {
        var service = new GitWorkingTreeService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CommitAsync(".", " ", "Test", "test@example.com"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CommitAsync(".", "message", "", "test@example.com"));
    }
}
