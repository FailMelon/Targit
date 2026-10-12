using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitRemoteServiceTests
{
    [Fact]
    public async Task FetchFromLocalRemoteUpdatesTrackingBranch()
    {
        var root = Path.Combine(Path.GetTempPath(), "targit-fetch-" + Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(root, "source");
        var targetPath = Path.Combine(root, "target");
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(targetPath);
        try
        {
            Repository.Init(sourcePath);
            string sourceBranch;
            using (var source = new Repository(sourcePath))
            {
                File.WriteAllText(Path.Combine(sourcePath, "readme.txt"), "initial\n");
                Commands.Stage(source, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                source.Commit("Initial", signature, signature);
                sourceBranch = source.Head.FriendlyName;
            }
            Repository.Init(targetPath);
            var service = new GitRemoteService();
            await service.AddAsync(targetPath, "origin", sourcePath);
            await service.FetchAsync(targetPath, "origin");
            using var target = new Repository(targetPath);
            Assert.NotNull(target.Branches["origin/" + sourceBranch]);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task CanAddListAndRemoveRemote()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-remote-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitRemoteService();
            Assert.Empty(await service.ListAsync(path));
            await service.AddAsync(path, "origin", "https://example.invalid/team/repo.git");
            var remotes = await service.ListAsync(path);
            var remote = Assert.Single(remotes);
            Assert.Equal("origin", remote.Name);
            Assert.Equal("https://example.invalid/team/repo.git", remote.Url);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddAsync(path, "origin", "https://example.invalid/other.git"));
            await service.RemoveAsync(path, "origin");
            Assert.Empty(await service.ListAsync(path));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RemoveAsync(path, "origin"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
