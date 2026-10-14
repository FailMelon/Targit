using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class UpstreamTests
{
    [Fact]
    public void DefaultRemoteIsOriginOrTheOnlyRemote()
    {
        Assert.Equal("origin", GitUpstreamService.DefaultRemote(["upstream", "origin"]));
        Assert.Equal("fork", GitUpstreamService.DefaultRemote(["fork"]));
        Assert.Null(GitUpstreamService.DefaultRemote(["fork", "upstream"]));
        Assert.Null(GitUpstreamService.DefaultRemote([]));
    }

    [Fact]
    public async Task PushesToTheChosenUpstreamEvenWithADifferentName()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitllama-upstream-" + Guid.NewGuid().ToString("N"));
        var server = Path.Combine(root, "server.git");
        var path = Path.Combine(root, "work");
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(server, isBare: true);
            Repository.Init(path);
            string local;
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "a.txt"), "a");
                Commands.Stage(repository, "a.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
                repository.Network.Remotes.Add("origin", server);
                local = repository.Head.FriendlyName;
            }

            var upstreams = new GitUpstreamService();
            Assert.Null(await upstreams.ReadAsync(path));

            await upstreams.SetAsync(path, "origin", "feature/shared");
            var pending = await upstreams.ReadAsync(path);
            Assert.Equal(new GitUpstream("origin", "feature/shared", false, 0, 0), pending);
            await Assert.ThrowsAsync<InvalidOperationException>(() => upstreams.SetAsync(path, "nowhere", "x"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => upstreams.SetAsync(path, "origin", "bad name"));

            await new GitRemoteService().PushCurrentBranchAsync(path, "origin", remoteBranchName: "feature/shared");

            using (var bare = new Repository(server))
            {
                Assert.NotNull(bare.Branches["feature/shared"]);
                Assert.Null(bare.Branches[local]);
            }

            var published = await upstreams.ReadAsync(path);
            Assert.Equal("origin/feature/shared", published!.Name);
            Assert.True(published.Published);
            Assert.Equal((0, 0), (published.Ahead, published.Behind));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(root, true);
        }
    }
}
