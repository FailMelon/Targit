using LibGit2Sharp;
using Targit.Git;
using Xunit;
namespace Targit.Core.Tests;
public sealed class Phase37Tests
{
    [Fact]
    public async Task RepositoryIntegration()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-37-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "first\n");
                Commands.Stage(repo, "file.txt");
                repo.Commit("first", sig, sig);
                File.WriteAllText(Path.Combine(path, "file.txt"), "second\n");
                Commands.Stage(repo, "file.txt");
                repo.Commit("second", sig, sig);
            }
            var nodes = await new GitGraphLaneService().GetAsync(path); Assert.Equal(2, nodes.Count); Assert.Equal(0, nodes[0].Lane); Assert.Equal(0, nodes[1].Lane);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
    [Fact]
    public async Task ValidatesEdgeCases()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-37-edge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitGraphLaneService(); Assert.Empty(await service.GetAsync(path)); await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetAsync(path, 0));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

}
