using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitWorkingTreeCancellationTests
{
    [Fact]
    public async Task CancelledStageDoesNotModifyIndex()
    {
        var path = CreateRepository();
        try
        {
            File.WriteAllText(Path.Combine(path, "pending.txt"), "pending");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new GitWorkingTreeService().StageAsync(path, "pending.txt", cancellation.Token));
            using var repository = new Repository(path);
            Assert.Equal(FileStatus.NewInWorkdir, repository.RetrieveStatus("pending.txt"));
        }
        finally { DeleteRepository(path); }
    }

    [Fact]
    public async Task CancelledCommitLeavesHeadAndStagedChangesUntouched()
    {
        var path = CreateRepository();
        try
        {
            File.WriteAllText(Path.Combine(path, "pending.txt"), "pending");
            var service = new GitWorkingTreeService();
            await service.StageAsync(path, "pending.txt");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.CommitAsync(path, "message", "Tester", "tester@example.invalid", cancellation.Token));
            using var repository = new Repository(path);
            Assert.Null(repository.Head.Tip);
            Assert.True((repository.RetrieveStatus("pending.txt") & FileStatus.NewInIndex) != 0);
        }
        finally { DeleteRepository(path); }
    }

    private static string CreateRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-cancellation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Repository.Init(path);
        return path;
    }

    private static void DeleteRepository(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        Directory.Delete(path, true);
    }
}
