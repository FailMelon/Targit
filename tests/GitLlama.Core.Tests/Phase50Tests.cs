using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phase50Tests
{
    [Fact]
    public async Task ReadsHistoricalFileContentWithoutUsingWorkingTree()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-50-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string first;
            string second;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "a.txt"), "first\n");
                Commands.Stage(repo, "a.txt");
                first = repo.Commit("first", sig, sig).Sha;
                File.WriteAllText(Path.Combine(path, "a.txt"), "second\n");
                Commands.Stage(repo, "a.txt");
                second = repo.Commit("second", sig, sig).Sha;
                File.WriteAllText(Path.Combine(path, "a.txt"), "uncommitted\n");
            }
            var service = new GitCommitFileContentService();
            Assert.Equal("first\n", (await service.GetAsync(path, first, "a.txt")).Content);
            Assert.Equal("second\n", (await service.GetAsync(path, second, "a.txt")).Content);
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, " ", "a.txt"));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, first, " "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, first, "missing.txt"));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetAsync(path, first, "a.txt", 0));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(path, first, "a.txt", 1));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}