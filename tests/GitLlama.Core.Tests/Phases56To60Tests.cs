using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phases56To60Tests
{
    [Fact]
    public async Task ReviewAndDiscardPreserveStagedContent()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-56-60-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var file = Path.Combine(path, "sample.txt");
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(file, "original\n");
                Commands.Stage(repo, "sample.txt");
                repo.Commit("initial", sig, sig);
                File.WriteAllText(file, "staged\n");
                Commands.Stage(repo, "sample.txt");
                File.WriteAllText(file, "unstaged\n");
            }

            var service = new GitWorkingTreeReviewService();
            var review = await service.InspectAsync(path, "sample.txt");
            Assert.Contains("staged", review.StagedPatch);
            Assert.Contains("unstaged", review.UnstagedPatch);
            Assert.NotEmpty(review.UnstagedHunks);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.DiscardUnstagedAsync(path, "sample.txt", "no")
            );
            Assert.Equal("unstaged\n", File.ReadAllText(file));
            await service.DiscardUnstagedAsync(path, "sample.txt", "DISCARD");
            // Discard writes the file as checkout would, so line endings follow core.autocrlf.
            Assert.Equal("staged\n", File.ReadAllText(file).Replace("\r\n", "\n"));
            using var check = new Repository(path);
            Assert.True((check.RetrieveStatus("sample.txt") & FileStatus.ModifiedInIndex) != 0);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task DiscardRejectsUntrackedFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-56-untracked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            File.WriteAllText(Path.Combine(path, "new.txt"), "keep");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new GitWorkingTreeReviewService().DiscardUnstagedAsync(path, "new.txt", "DISCARD")
            );
            Assert.True(File.Exists(Path.Combine(path, "new.txt")));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }
}
