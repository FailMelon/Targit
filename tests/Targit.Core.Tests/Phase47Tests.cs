using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class Phase47Tests
{
    [Fact]
    public async Task ListsChangedFilesForRootAndSubsequentCommits()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-47-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string first;
            string second;
            using (var repository = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "a.txt"), "one");
                Commands.Stage(repository, "a.txt");
                first = repository.Commit("first", sig, sig).Sha;
                File.WriteAllText(Path.Combine(path, "b.txt"), "two");
                Commands.Stage(repository, "b.txt");
                File.WriteAllText(Path.Combine(path, "a.txt"), "updated");
                Commands.Stage(repository, "a.txt");
                second = repository.Commit("second", sig, sig).Sha;
            }
            var service = new GitCommitChangedFilesService();
            var root = await service.GetAsync(path, first);
            Assert.Equal("a.txt", Assert.Single(root).Path);
            var files = await service.GetAsync(path, second);
            Assert.Equal(new[] { "a.txt", "b.txt" }, files.Select(file => file.Path).ToArray());
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