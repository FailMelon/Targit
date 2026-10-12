using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitStashServiceTests
{
    [Fact]
    public async Task EmptyRepositoryHasNoStashes()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-stashes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            Assert.Empty(await new GitStashService().ListAsync(path));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
    [Fact]
    public async Task PopRestoresChangesAndRemovesStash()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-pop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var r = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "file.txt"), "original");
                Commands.Stage(r, "file.txt");
                var s = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                r.Commit("Initial", s, s);
                File.WriteAllText(Path.Combine(path, "file.txt"), "changed");
                r.Stashes.Add(s, "saved");
            }
            var service = new GitStashService();
            await service.PopAsync(path, 0);
            Assert.Equal("changed", File.ReadAllText(Path.Combine(path, "file.txt")));
            Assert.Empty(await service.ListAsync(path));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, File.GetAttributes(f) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

}
