using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitTagServiceTests
{
    [Fact]
    public async Task ListsLightweightTag()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-tags-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string sha;
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "hello");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                sha = repository.Commit("Initial", signature, signature).Sha;
                repository.Tags.Add("v1.0", repository.Head.Tip);
            }
            var tag = Assert.Single(await new GitTagService().ListAsync(path));
            Assert.Equal("v1.0", tag.Name);
            Assert.Equal(sha, tag.TargetSha);
            Assert.False(tag.IsAnnotated);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
    [Fact]
    public async Task CreatesLightweightTagAndRejectsDuplicate()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-create-tag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "hello");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }
            var service = new GitTagService();
            await service.CreateAsync(path, "v2.0");
            Assert.Equal("v2.0", Assert.Single(await service.ListAsync(path)).Name);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(path, "v2.0"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

}
