using LibGit2Sharp;
using Targit.Git;
using Xunit;
namespace Targit.Core.Tests;
public sealed class Phase41Tests
{
    [Fact]
    public async Task RepositoryIntegration()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-41-" + Guid.NewGuid().ToString("N"));
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
            using (var repo = new Repository(path)) repo.Tags.Add("v1", repo.Head.Tip); var tags = await new GitTagInspectionService().GetAsync(path); Assert.Equal("v1", Assert.Single(tags).Name);
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
        var path = Path.Combine(Path.GetTempPath(), "targit-41-edge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitTagInspectionService(); Assert.Empty(await service.GetAsync(path)); using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(path, cancelled.Token));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

}
