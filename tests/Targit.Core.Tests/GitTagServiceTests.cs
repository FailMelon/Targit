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
    public async Task CreatesAnnotatedTagWithoutOverwriting()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-annotated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var r = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "file.txt"), "hello");
                Commands.Stage(r, "file.txt");
                var s = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                r.Commit("Initial", s, s);
            }
            var service = new GitTagService();
            await service.CreateAnnotatedAsync(path, "v2", "Release 2", "Tester", "test@example.invalid");
            var tag = Assert.Single(await service.ListAsync(path));
            Assert.True(tag.IsAnnotated);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAnnotatedAsync(path, "v2", "again", "Tester", "test@example.invalid"));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, File.GetAttributes(f) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

}
