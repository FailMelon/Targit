using LibGit2Sharp;
using Targit.Git;
using Xunit;
namespace Targit.Core.Tests;
public sealed class Phase39Tests
{
    [Fact]
    public async Task RepositoryIntegration()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-39-" + Guid.NewGuid().ToString("N"));
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
            var lines = await new GitFileBlameService().GetAsync(path, "file.txt"); Assert.NotEmpty(lines); Assert.All(lines, line => Assert.True(line.LineCount > 0));
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
        var path = Path.Combine(Path.GetTempPath(), "targit-39-edge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitFileBlameService(); await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, "../outside.txt")); await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(path, " "));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

}
