using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitTagServiceTests
{
    [Fact]
    public async Task ListsLightweightTag()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-tags-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string sha;
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "hello");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                sha = repository.Commit("Initial", signature, signature).Sha;
                repository.Tags.Add("v1.0", repository.Head.Tip);
            }
            var tag = Assert.Single(await new GitTagService().ListAsync(path));
            Assert.Equal("v1.0", tag.Name);
            Assert.Equal(sha, tag.TargetSha);
            Assert.False(tag.IsAnnotated);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
    [Fact]
    public async Task CreatesLightweightTagAndRejectsDuplicate()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-create-tag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "hello");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }
            var service = new GitTagService();
            await service.CreateAsync(path, "v2.0");
            Assert.Equal("v2.0", Assert.Single(await service.ListAsync(path)).Name);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(path, "v2.0"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }


    [Fact]
    public async Task DeletesOnlyExistingLocalTag()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-delete-tag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "hello");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
                repository.Tags.Add("v1", repository.Head.Tip);
            }
            var service = new GitTagService();
            await service.DeleteAsync(path, "v1");
            Assert.Empty(await service.ListAsync(path));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(path, "v1"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task CreatesAnnotatedTagWithoutOverwriting()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-annotated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var r = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "file.txt"), "hello");
                Commands.Stage(r, "file.txt");
                var s = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                r.Commit("Initial", s, s);
            }
            var service = new GitTagService();
            await service.CreateAnnotatedAsync(path, "v2", "Release 2", "Tester", "test@example.invalid");
            var tag = Assert.Single(await service.ListAsync(path));
            Assert.True(tag.IsAnnotated);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAnnotatedAsync(path, "v2", "again", "Tester", "test@example.invalid"));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, File.GetAttributes(f) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task PushAndDeleteRemoteTagWithoutDeletingLocalTag()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitllama-remote-tags-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var clone = Path.Combine(root, "clone");
        var remote = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(source);
        try
        {
            Repository.Init(source);
            using (var r = new Repository(source))
            {
                File.WriteAllText(Path.Combine(source, "file.txt"), "hello");
                Commands.Stage(r, "file.txt");
                var s = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                r.Commit("Initial", s, s);
            }
            Repository.Init(remote, isBare: true);
            Repository.Clone(source, clone);
            using (var r = new Repository(clone))
                r.Network.Remotes.Update("origin", updater => updater.Url = remote);
            using (var r = new Repository(clone))
                r.Tags.Add("v2", r.Head.Tip);
            var service = new GitTagService();
            await service.PushAsync(clone, "origin", "v2");
            using (var r = new Repository(remote)) Assert.NotNull(r.Tags["v2"]);
            await service.DeleteRemoteAsync(clone, "origin", "v2");
            using (var r = new Repository(remote)) Assert.Null(r.Tags["v2"]);
            using (var r = new Repository(clone)) Assert.NotNull(r.Tags["v2"]);
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, File.GetAttributes(f) & ~FileAttributes.ReadOnly);
            Directory.Delete(root, true);
        }
    }
}
