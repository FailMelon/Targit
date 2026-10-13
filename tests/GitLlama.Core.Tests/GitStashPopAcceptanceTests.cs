using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitStashPopAcceptanceTests
{
    [Fact]
    public async Task PopRestoresChangesAndRemovesEntryOnlyAfterSuccess()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-pop-stash-" + Guid.NewGuid().ToString("N"));
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
            File.WriteAllText(Path.Combine(path, "tracked.txt"), "stashed");
            await service.SaveAsync(path, "pop me");
            File.WriteAllText(Path.Combine(path, "tracked.txt"), "dirty");
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PopAsync(path, 0));
            Assert.Single(await service.ListAsync(path));
            Assert.Equal("dirty", File.ReadAllText(Path.Combine(path, "tracked.txt")));
            File.WriteAllText(Path.Combine(path, "tracked.txt"), "original");
            await service.PopAsync(path, 0);
            Assert.Equal("stashed", File.ReadAllText(Path.Combine(path, "tracked.txt")));
            Assert.Empty(await service.ListAsync(path));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
