using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitAnnotatedTagAcceptanceTests
{
    [Fact]
    public async Task CreatesAnnotatedTagWithMessageAndTaggerWithoutOverwriting()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-annotated-tag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string sha;
            using (var repo = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "tracked.txt"), "content");
                Commands.Stage(repo, "tracked.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                sha = repo.Commit("initial", signature, signature).Sha;
            }
            var service = new GitTagService();
            await service.CreateAnnotatedAsync(path, "v2.0.0", "Release notes", "Tagger", "tagger@example.invalid");
            var info = Assert.Single(await service.ListAsync(path));
            Assert.Equal("v2.0.0", info.Name);
            Assert.True(info.IsAnnotated);
            Assert.Equal(sha, info.TargetSha);
            using (var repo = new Repository(path))
            {
                var annotation = repo.Tags["v2.0.0"]!.Annotation!;
                Assert.Contains("Release notes", annotation.Message);
                Assert.Equal("Tagger", annotation.Tagger.Name);
                Assert.Equal("tagger@example.invalid", annotation.Tagger.Email);
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAnnotatedAsync(path, "v2.0.0", "again", "Tagger", "tagger@example.invalid"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
