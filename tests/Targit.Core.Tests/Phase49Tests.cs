using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class Phase49Tests
{
    [Fact]
    public async Task ReadsRootAndSubsequentCommitFilePatches()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-49-" + Guid.NewGuid().ToString("N"));
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
                second = repo.Commit("second", sig, sig).Sha;
            }
            var service = new GitCommitFilePatchService();
            var root = await service.GetAsync(path, first, "a.txt");
            Assert.Equal(2, root.LinesAdded);
            Assert.Contains("+one", root.PatchText);
            var modified = await service.GetAsync(path, second, "a.txt");
            Assert.Equal(1, modified.LinesAdded);
            Assert.Equal(1, modified.LinesDeleted);
            Assert.Contains("+three", modified.PatchText);
            Assert.Contains("-two", modified.PatchText);
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, " ", "a.txt"));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, second, " "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, second, "missing.txt"));
            await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, new string('0', 40), "a.txt"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}