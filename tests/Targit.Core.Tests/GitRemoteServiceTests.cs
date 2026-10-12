using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitRemoteServiceTests
{
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
