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
    public async Task SavesTrackedChangesAndRejectsCleanRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-save-stash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "initial");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }
            var service = new GitStashService();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(path, "empty"));
            File.WriteAllText(Path.Combine(path, "readme.txt"), "modified");
            await service.SaveAsync(path, "work in progress");
            Assert.Single(await service.ListAsync(path));
            Assert.Equal("initial", File.ReadAllText(Path.Combine(path, "readme.txt")));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }


    [Fact]
    public async Task DropsSelectedStashAndRejectsMissingIndex()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-drop-stash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "initial");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
                File.WriteAllText(Path.Combine(path, "readme.txt"), "changed");
                repository.Stashes.Add(signature, "save");
            }
            var service = new GitStashService();
            Assert.Single(await service.ListAsync(path));
            await service.DropAsync(path, 0);
            Assert.Empty(await service.ListAsync(path));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DropAsync(path, 0));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
