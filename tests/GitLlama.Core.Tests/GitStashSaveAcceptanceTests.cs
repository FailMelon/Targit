using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitStashSaveAcceptanceTests
{
    [Fact]
    public async Task StashesOnlyStagedChangesAndLeavesUnstagedAndUntrackedFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-stash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repo = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "staged.txt"), "original");
                File.WriteAllText(Path.Combine(path, "unstaged.txt"), "original");
                Commands.Stage(repo, "staged.txt");
                Commands.Stage(repo, "unstaged.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repo.Commit("initial", signature, signature);
            }

            File.WriteAllText(Path.Combine(path, "unstaged.txt"), "keep editing");
            File.WriteAllText(Path.Combine(path, "untracked.txt"), "leave me");
            var service = new GitStashService();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveAsync(path, "nothing staged", "Test", "test@example.invalid")
            );
            Assert.Empty(await service.ListAsync(path));

            File.WriteAllText(Path.Combine(path, "staged.txt"), "ready");
            using (var repo = new Repository(path))
            {
                Commands.Stage(repo, "staged.txt");
            }

            await service.SaveAsync(path, "phase 19", "Test", "test@example.invalid");
            var stash = Assert.Single(await service.ListAsync(path));
            Assert.Equal(0, stash.Index);
            Assert.Contains("phase 19", stash.Message);
            Assert.Equal("original", File.ReadAllText(Path.Combine(path, "staged.txt")));
            Assert.Equal("keep editing", File.ReadAllText(Path.Combine(path, "unstaged.txt")));
            Assert.Equal("leave me", File.ReadAllText(Path.Combine(path, "untracked.txt")));
            using (var repo = new Repository(path))
            {
                Assert.Equal(FileStatus.ModifiedInWorkdir, repo.RetrieveStatus("unstaged.txt"));
                Assert.Equal(FileStatus.Unaltered, repo.RetrieveStatus("staged.txt"));
            }
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
