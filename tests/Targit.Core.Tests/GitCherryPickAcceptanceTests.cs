using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitCherryPickAcceptanceTests
{
    [Fact]
    public async Task CherryPickAppliesCommitAndRefusesDirtyWorkingTree()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-cherry-pick-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string commitSha;
            using (var repo = new Repository(path))
            {
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "base.txt"), "base");
                Commands.Stage(repo, "base.txt");
                repo.Commit("base", signature, signature);
                repo.CreateBranch("feature");
                File.WriteAllText(Path.Combine(path, "feature.txt"), "picked");
                Commands.Stage(repo, "feature.txt");
                commitSha = repo.Commit("feature change", signature, signature).Sha;
                Commands.Checkout(repo, repo.Branches["feature"]);
            }
            var service = new GitCherryPickService();
            File.WriteAllText(Path.Combine(path, "base.txt"), "dirty");
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CherryPickAsync(path, commitSha));
            File.WriteAllText(Path.Combine(path, "base.txt"), "base");
            var newSha = await service.CherryPickAsync(path, commitSha);
            Assert.Equal("picked", File.ReadAllText(Path.Combine(path, "feature.txt")));
            using (var repo = new Repository(path))
            {
                Assert.Equal(newSha, repo.Head.Tip!.Sha);
                Assert.Equal("feature change", repo.Head.Tip.MessageShort);
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
