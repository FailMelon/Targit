using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phase48Tests
{
    [Fact]
    public async Task ReportsPerFileAdditionsAndDeletions()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-48-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string first;
            string second;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "a.txt"), "one\ntwo\n");
                Commands.Stage(repo, "a.txt");
                first = repo.Commit("first", sig, sig).Sha;
                File.WriteAllText(Path.Combine(path, "a.txt"), "one\nthree\n");
                Commands.Stage(repo, "a.txt");
                File.WriteAllText(Path.Combine(path, "b.txt"), "new\n");
                Commands.Stage(repo, "b.txt");
                second = repo.Commit("second", sig, sig).Sha;
            }
            var service = new GitCommitFileStatisticsService();
            var root = Assert.Single(await service.GetAsync(path, first));
            Assert.Equal("a.txt", root.Path);
            Assert.Equal(2, root.LinesAdded);
            Assert.Equal(0, root.LinesDeleted);
            var files = await service.GetAsync(path, second);
            Assert.Equal(new[] { "a.txt", "b.txt" }, files.Select(f => f.Path).ToArray());
            Assert.Equal(1, files[0].LinesAdded);
            Assert.Equal(1, files[0].LinesDeleted);
            Assert.Equal(1, files[1].LinesAdded);
            Assert.Single(await service.GetAsync(path, second, 1));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, " "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, new string('0', 40)));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetAsync(path, second, 0));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}