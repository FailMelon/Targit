using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitCommitInspectionAcceptanceTests
{
    [Fact]
    public async Task ReturnsBoundedCommitHistoryWithAuthorAndTimestamp()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-inspection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var signature = new Signature("Inspection Tester", "inspection@example.invalid", DateTimeOffset.UtcNow);
            using (var repo = new Repository(path))
            {
                for (var i = 0; i < 3; i++)
                {
                    File.WriteAllText(Path.Combine(path, "file.txt"), i.ToString());
                    Commands.Stage(repo, "file.txt");
                    repo.Commit("commit " + i, signature, signature);
                }
            }
            var service = new GitCommitInspectionService();
            var commits = await service.ListRecentAsync(path, 2);
            Assert.Equal(2, commits.Count);
            Assert.Equal("commit 2", commits[0].Message);
            Assert.All(commits, c =>
            {
                Assert.Equal(40, c.Sha.Length);
                Assert.Equal("Inspection Tester", c.Author);
                Assert.Equal(signature.When.ToUnixTimeSeconds(), c.When.ToUnixTimeSeconds());
            });
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListRecentAsync(path, 0));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListRecentAsync(path, 501));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
