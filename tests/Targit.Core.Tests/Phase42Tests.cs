using LibGit2Sharp;
using Targit.Git;
using Xunit;
namespace Targit.Core.Tests;
public sealed class Phase42Tests
{
    [Fact]
    public async Task InspectCommitParentsAndMissingSha()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-42-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string firstSha;
            string secondSha;
            using (var repo = new Repository(path))
            {
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "first");
                Commands.Stage(repo, "file.txt");
                firstSha = repo.Commit("first", signature, signature).Sha;
                File.WriteAllText(Path.Combine(path, "file.txt"), "second");
                Commands.Stage(repo, "file.txt");
                secondSha = repo.Commit("second", signature, signature).Sha;
            }
            var service = new GitCommitAncestryService();
            var root = await service.InspectAsync(path, firstSha);
            Assert.Empty(root.Parents);
            Assert.False(root.IsMergeCommit);
            var second = await service.InspectAsync(path, secondSha);
            Assert.Equal("second", second.Subject);
            Assert.Equal(firstSha, Assert.Single(second.Parents));
            Assert.False(second.IsMergeCommit);
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, " "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, new string('0', 40)));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}