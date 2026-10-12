using LibGit2Sharp;
using Targit.Git;
using Xunit;
namespace Targit.Core.Tests;
public sealed class Phase38Tests
{
    [Fact]
    public async Task RepositoryIntegration()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-38-" + Guid.NewGuid().ToString("N"));
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
            var hits = await new GitCommitSearchService().SearchAsync(path, "second"); Assert.Equal("second", Assert.Single(hits).Subject); Assert.Empty(await new GitCommitSearchService().SearchAsync(path, "absent"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
