using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class Phase43Tests
{
    [Fact]
    public async Task FindsReachableDescendantsAndHonorsLimit()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-43-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string first;
            string second;
            string third;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "first");
                Commands.Stage(repo, "file.txt");
                first = repo.Commit("first", sig, sig).Sha;
                File.WriteAllText(Path.Combine(path, "file.txt"), "second");
                Commands.Stage(repo, "file.txt");
                second = repo.Commit("second", sig, sig).Sha;
                File.WriteAllText(Path.Combine(path, "file.txt"), "third");
                Commands.Stage(repo, "file.txt");
                third = repo.Commit("third", sig, sig).Sha;
            }

            var service = new GitCommitDescendantService();
            var descendants = await service.GetAsync(path, first);
            Assert.Equal(2, descendants.Count);
            Assert.Equal(second, descendants[0].Sha);
            Assert.Equal(1, descendants[0].Distance);
            Assert.Equal(third, descendants[1].Sha);
            Assert.Equal(2, descendants[1].Distance);
            Assert.Empty(await service.GetAsync(path, third));
            Assert.Equal(second, Assert.Single(await service.GetAsync(path, first, 1)).Sha);
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, " "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, new string('0', 40)));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetAsync(path, first, 0));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}