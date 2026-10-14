using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phase46Tests
{
    [Fact]
    public async Task ReportsRootAndRegularCommitStatistics()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-46-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string first;
            string second;
            using (var repository = new Repository(path))
            {
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "one\n");
                Commands.Stage(repository, "file.txt");
                first = repository.Commit("first", signature, signature).Sha;
                File.WriteAllText(Path.Combine(path, "file.txt"), "one\ntwo\n");
                Commands.Stage(repository, "file.txt");
                second = repository.Commit("second", signature, signature).Sha;
            }

            var service = new GitCommitDiffStatisticsService();
            var root = await service.InspectAsync(path, first);
            Assert.True(root.IsRoot);
            Assert.Equal(1, root.FilesChanged);
            Assert.Equal(1, root.LinesAdded);
            Assert.Equal(0, root.LinesDeleted);
            var regular = await service.InspectAsync(path, second);
            Assert.False(regular.IsRoot);
            Assert.Equal(1, regular.FilesChanged);
            Assert.Equal(1, regular.LinesAdded);
            Assert.Equal(0, regular.LinesDeleted);
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, " "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, new string('0', 40)));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }
}
