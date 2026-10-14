using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitStashSaveAcceptanceTests
{
    [Fact]
    public async Task StashesTrackedChangesButLeavesUntrackedFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-stash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repo = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "tracked.txt"), "original");
                Commands.Stage(repo, "tracked.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repo.Commit("initial", signature, signature);
            }

            File.WriteAllText(Path.Combine(path, "tracked.txt"), "modified");
            File.WriteAllText(Path.Combine(path, "untracked.txt"), "leave me");
            var service = new GitStashService();
            await service.SaveAsync(path, "phase 19");
            var stash = Assert.Single(await service.ListAsync(path));
            Assert.Equal(0, stash.Index);
            Assert.Contains("phase 19", stash.Message);
            Assert.Equal("original", File.ReadAllText(Path.Combine(path, "tracked.txt")));
            Assert.Equal("leave me", File.ReadAllText(Path.Combine(path, "untracked.txt")));
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
