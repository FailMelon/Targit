using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitTagDeletionAcceptanceTests
{
    [Fact]
    public async Task DeletingLocalTagPreservesCommitAndOtherTags()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-delete-tag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string sha;
            using (var repo = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "file.txt"), "content");
                Commands.Stage(repo, "file.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                sha = repo.Commit("initial", signature, signature).Sha;
            }
            var service = new GitTagService();
            await service.CreateAsync(path, "delete-me");
            await service.CreateAsync(path, "keep-me");
            await service.DeleteAsync(path, "delete-me");
            var remaining = Assert.Single(await service.ListAsync(path));
            Assert.Equal("keep-me", remaining.Name);
            Assert.Equal(sha, remaining.TargetSha);
            using (var repo = new Repository(path))
                Assert.Equal(sha, repo.Head.Tip!.Sha);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(path, "delete-me"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
