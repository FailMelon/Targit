using GitLlama.Git;
using LibGit2Sharp;
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
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task AppliesAndPopsOverLocalChangesOnlyWhenAllowedAndNotOverlapping()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-dirty-stash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "stashed.txt"), "initial");
                File.WriteAllText(Path.Combine(path, "local.txt"), "initial");
                Commands.Stage(repository, "*");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }

            var service = new GitStashService();
            File.WriteAllText(Path.Combine(path, "stashed.txt"), "from stash");
            await service.SaveAsync(path, "work", "Test", "test@example.invalid");

            // A stash that touches the same file as local changes is refused and nothing is lost.
            File.WriteAllText(Path.Combine(path, "stashed.txt"), "local edit");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.PopAsync(path, 0, allowLocalChanges: true)
            );
            Assert.Equal("local edit", File.ReadAllText(Path.Combine(path, "stashed.txt")));
            Assert.Single(await service.ListAsync(path));

            // Changes to other files are kept alongside the stash.
            File.WriteAllText(Path.Combine(path, "stashed.txt"), "initial");
            File.WriteAllText(Path.Combine(path, "local.txt"), "local edit");
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyAsync(path, 0));
            await service.ApplyAsync(path, 0, allowLocalChanges: true);
            Assert.Equal("from stash", File.ReadAllText(Path.Combine(path, "stashed.txt")));
            Assert.Equal("local edit", File.ReadAllText(Path.Combine(path, "local.txt")));
            Assert.Single(await service.ListAsync(path));

            File.WriteAllText(Path.Combine(path, "stashed.txt"), "initial");
            await service.PopAsync(path, 0, allowLocalChanges: true);
            Assert.Equal("from stash", File.ReadAllText(Path.Combine(path, "stashed.txt")));
            Assert.Equal("local edit", File.ReadAllText(Path.Combine(path, "local.txt")));
            Assert.Empty(await service.ListAsync(path));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task ReportsChangesTheStashLeavesBehind()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-leftover-stash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                repository.Config.Set("core.autocrlf", true);
                File.WriteAllText(Path.Combine(path, "real.txt"), "one\n");
                File.WriteAllText(Path.Combine(path, "endings.txt"), "one\ntwo\n");
                Commands.Stage(repository, "*");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }

            var service = new GitStashService();
            File.WriteAllText(Path.Combine(path, "endings.txt"), "one\r\ntwo\r\n");
            File.WriteAllText(Path.Combine(path, "new.txt"), "untracked");
            var nothing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveAsync(path, "nothing real", "Test", "test@example.invalid")
            );
            Assert.StartsWith("Nothing could be stashed.", nothing.Message);
            Assert.Contains("only line-ending differences", nothing.Message);
            Assert.Empty(await service.ListAsync(path));

            File.WriteAllText(Path.Combine(path, "real.txt"), "two\n");
            var result = await service.SaveAsync(path, "work", "Test", "test@example.invalid");
            Assert.Equal(1, result.StashedFiles);
            Assert.Equal(["new.txt"], result.Untracked);
            Assert.Contains("1 untracked file", GitStashService.Describe(result));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task StashesUntrackedFilesOnlyWhenAsked()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-untracked-stash-" + Guid.NewGuid().ToString("N"));
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
            var added = Path.Combine(path, "new.txt");
            File.WriteAllText(added, "untracked");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveAsync(path, "tracked only", "Test", "test@example.invalid")
            );
            Assert.Empty(await service.ListAsync(path));

            await service.SaveAsync(path, "everything", "Test", "test@example.invalid", includeUntracked: true);
            Assert.Single(await service.ListAsync(path));
            Assert.False(File.Exists(added));

            await service.ApplyAsync(path, 0);
            Assert.Equal("untracked", File.ReadAllText(added));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

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
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveAsync(path, "empty", "Test", "test@example.invalid")
            );
            File.WriteAllText(Path.Combine(path, "readme.txt"), "modified");
            await service.SaveAsync(path, "work in progress", "Test", "test@example.invalid");
            var stash = Assert.Single(await service.ListAsync(path));
            using (var repository = new Repository(path))
            {
                Assert.Equal(repository.Stashes.Single().WorkTree.Sha, stash.Sha);
                Assert.Equal("test@example.invalid", repository.Stashes.Single().WorkTree.Author.Email);
            }

            Assert.Equal("initial", File.ReadAllText(Path.Combine(path, "readme.txt")));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

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
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

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
            {
                File.SetAttributes(f, File.GetAttributes(f) & ~FileAttributes.ReadOnly);
            }

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
            {
                File.SetAttributes(f, File.GetAttributes(f) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }
}
