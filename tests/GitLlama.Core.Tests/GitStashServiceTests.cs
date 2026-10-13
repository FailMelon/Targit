using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitStashServiceTests
{
    [Fact]
    public async Task EmptyRepositoryHasNoStashes()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-stashes-" + Guid.NewGuid().ToString("N"));
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
        var path = Path.Combine(Path.GetTempPath(), "gitllama-save-stash-" + Guid.NewGuid().ToString("N"));
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
        var path = Path.Combine(Path.GetTempPath(), "gitllama-drop-stash-" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public async Task ApplyRestoresChangesAndRetainsStash()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-apply-" + Guid.NewGuid().ToString("N"));
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
            await service.ApplyAsync(path, 0);
            Assert.Equal("changed", File.ReadAllText(Path.Combine(path, "file.txt")));
            Assert.Single(await service.ListAsync(path));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyAsync(path, 0));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, File.GetAttributes(f) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task PopRestoresChangesAndRemovesStash()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-pop-" + Guid.NewGuid().ToString("N"));
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
