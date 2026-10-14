using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitStashDropAcceptanceTests
{
    [Fact]
    public async Task DropsOnlySelectedStashWithoutApplyingChanges()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-drop-stash-" + Guid.NewGuid().ToString("N"));
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

            var service = new GitStashService();
            File.WriteAllText(Path.Combine(path, "tracked.txt"), "first");
            using (var repo = new Repository(path))
            {
                Commands.Stage(repo, "tracked.txt");
            }

            await service.SaveStagedAsync(path, "first stash");
            File.WriteAllText(Path.Combine(path, "tracked.txt"), "second");
            using (var repo = new Repository(path))
            {
                Commands.Stage(repo, "tracked.txt");
            }

            await service.SaveStagedAsync(path, "second stash");
            Assert.Equal(2, (await service.ListAsync(path)).Count);
            await service.DropAsync(path, 0);
            var remaining = Assert.Single(await service.ListAsync(path));
            Assert.Contains("first stash", remaining.Message);
            Assert.Equal("original", File.ReadAllText(Path.Combine(path, "tracked.txt")));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DropAsync(path, 5));
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
