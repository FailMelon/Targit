using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitCommitInspectionServiceTests
{
    [Fact]
    public async Task ListsRecentCommitsAndEnforcesLimit()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-commits-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitCommitInspectionService();
            Assert.Empty(await service.ListRecentAsync(path));
            using (var repository = new Repository(path))
            {
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                for (var i = 0; i < 3; i++)
                {
                    File.WriteAllText(Path.Combine(path, "readme.txt"), i.ToString());
                    Commands.Stage(repository, "readme.txt");
                    repository.Commit($"Commit {i}", signature, signature);
                }
            }
            var commits = await service.ListRecentAsync(path, 2);
            Assert.Equal(2, commits.Count);
            Assert.All(commits, commit => Assert.Equal("Test", commit.Author));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListRecentAsync(path, 0));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
