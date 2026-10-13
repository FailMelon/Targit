using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitLightweightTagAcceptanceTests
{
    [Fact]
    public async Task CreateLightweightTagTargetsHeadAndRejectsDuplicates()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-tag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string sha;
            using (var repo = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "test");
                Commands.Stage(repo, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                sha = repo.Commit("initial", signature, signature).Sha;
            }
            var service = new GitTagService();
            await service.CreateAsync(path, "v1.0.0");
            var tag = Assert.Single(await service.ListAsync(path));
            Assert.Equal("v1.0.0", tag.Name);
            Assert.Equal(sha, tag.TargetSha);
            Assert.False(tag.IsAnnotated);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(path, "v1.0.0"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
