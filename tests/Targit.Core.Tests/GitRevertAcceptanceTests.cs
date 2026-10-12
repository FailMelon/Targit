using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitRevertAcceptanceTests
{
    [Fact]
    public async Task RevertCreatesInverseCommitAndRefusesDirtyWorkingTree()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-revert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string targetSha;
            using (var repo = new Repository(path))
            {
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "original");
                Commands.Stage(repo, "file.txt");
                repo.Commit("base", signature, signature);
                File.WriteAllText(Path.Combine(path, "file.txt"), "changed");
                Commands.Stage(repo, "file.txt");
                targetSha = repo.Commit("change", signature, signature).Sha;
            }
            var service = new GitRevertService();
            File.WriteAllText(Path.Combine(path, "file.txt"), "dirty");
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevertAsync(path, targetSha));
            File.WriteAllText(Path.Combine(path, "file.txt"), "changed");
            var revertSha = await service.RevertAsync(path, targetSha);
            Assert.Equal("original", File.ReadAllText(Path.Combine(path, "file.txt")));
            using (var repo = new Repository(path))
            {
                Assert.Equal(revertSha, repo.Head.Tip!.Sha);
                Assert.Contains("Revert", repo.Head.Tip.MessageShort);
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
