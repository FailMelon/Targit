using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitAmendServiceTests
{
    [Fact]
    public async Task AmendChangesMessageAndPreservesParent()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-amend-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string oldSha;
            using (var r = new Repository(path))
            {
                var s = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "initial");
                Commands.Stage(r, "file.txt");
                r.Commit("Initial", s, s);
                File.WriteAllText(Path.Combine(path, "file.txt"), "second");
                Commands.Stage(r, "file.txt");
                oldSha = r.Commit("Old message", s, s).Sha;
            }
            var service = new GitAmendService();
            var newSha = await service.AmendMessageAsync(path, "Updated message");
            Assert.NotEqual(oldSha, newSha);
            using (var r = new Repository(path))
            {
                Assert.Equal("Updated message", r.Head.Tip?.MessageShort);
                Assert.Equal("Initial", r.Head.Tip?.Parents.Single().MessageShort);
            }
            File.WriteAllText(Path.Combine(path, "file.txt"), "dirty");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AmendMessageAsync(path, "Should fail"));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, File.GetAttributes(f) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
