using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitRemoteServiceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PullFastForwardsAndProtectsLocalWork(bool dirty, bool divergent)
    {
        var root = Path.Combine(Path.GetTempPath(), "targit-pull-ff-" + Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(root, "source");
        var clonePath = Path.Combine(root, "clone");
        Directory.CreateDirectory(sourcePath);
        try
        {
            Repository.Init(sourcePath);
            var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
            using (var source = new Repository(sourcePath))
            {
                File.WriteAllText(Path.Combine(sourcePath, "readme.txt"), "initial\n");
                Commands.Stage(source, "readme.txt");
                source.Commit("Initial", signature, signature);
            }
            Repository.Clone(sourcePath, clonePath);
            string initialSha;
            using (var clone = new Repository(clonePath))
            {
                initialSha = clone.Head.Tip!.Sha;
                if (divergent)
                {
                    File.WriteAllText(Path.Combine(clonePath, "local.txt"), "local\n");
                    Commands.Stage(clone, "local.txt");
                    clone.Commit("Local", signature, signature);
                }
            }
            string remoteSha;
            using (var source = new Repository(sourcePath))
            {
                File.WriteAllText(Path.Combine(sourcePath, "remote.txt"), "remote\n");
                Commands.Stage(source, "remote.txt");
                remoteSha = source.Commit("Remote", signature, signature).Sha;
            }
            if (dirty)
                File.WriteAllText(Path.Combine(clonePath, "dirty.txt"), "uncommitted\n");
            var service = new GitRemoteService();
            if (dirty)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.PullFastForwardAsync(clonePath));
            }
            else if (divergent)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => service.PullFastForwardAsync(clonePath));
            }
            else
            {
                await service.PullFastForwardAsync(clonePath);
            }
            using var result = new Repository(clonePath);
            if (dirty) Assert.Equal(initialSha, result.Head.Tip!.Sha);
            else if (divergent) Assert.NotEqual(remoteSha, result.Head.Tip!.Sha);
            else
            {
                Assert.Equal(remoteSha, result.Head.Tip!.Sha);
                Assert.True(File.Exists(Path.Combine(clonePath, "remote.txt")));
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task PullRejectsDetachedHeadBeforeNetworkAccess()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-pull-detached-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "initial\n");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                var commit = repository.Commit("Initial", signature, signature);
                Commands.Checkout(repository, commit);
            }
            var service = new GitRemoteService();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.PullFastForwardAsync(path));
            Assert.Contains("checked-out branch", error.Message);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task UpstreamConfigurationRejectsDetachedHead()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-upstream-detached-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "initial\n");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                var commit = repository.Commit("Initial", signature, signature);
                Commands.Checkout(repository, commit);
            }
            var service = new GitRemoteService();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SetCurrentBranchUpstreamAsync(path, "origin", "main"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ClearCurrentBranchUpstreamAsync(path));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task UpstreamConfigurationRejectsMissingRemoteAndUnbornHead()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-upstream-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitRemoteService();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SetCurrentBranchUpstreamAsync(path, "origin", "main"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ClearCurrentBranchUpstreamAsync(path));

            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "initial\n");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SetCurrentBranchUpstreamAsync(path, "missing", "main"));
            Assert.Contains("does not exist", error.Message);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task CanSetAndClearCurrentBranchUpstream()
    {
        var root = Path.Combine(Path.GetTempPath(), "targit-upstream-" + Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(root, "source");
        var localPath = Path.Combine(root, "local");
        Directory.CreateDirectory(sourcePath);
        try
        {
            Repository.Init(sourcePath);
            using (var source = new Repository(sourcePath))
            {
                File.WriteAllText(Path.Combine(sourcePath, "readme.txt"), "initial\n");
                Commands.Stage(source, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                source.Commit("Initial", signature, signature);
            }
            Repository.Clone(sourcePath, localPath);
            string branchName;
            using (var local = new Repository(localPath))
                branchName = local.Head.FriendlyName;
            var service = new GitRemoteService();
            await service.ClearCurrentBranchUpstreamAsync(localPath);
            using (var local = new Repository(localPath))
                Assert.False(local.Head.IsTracking);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SetCurrentBranchUpstreamAsync(localPath, "origin", "nonexistent"));
            await service.SetCurrentBranchUpstreamAsync(localPath, "origin", branchName);
            using (var local = new Repository(localPath))
            {
                Assert.True(local.Head.IsTracking);
                Assert.Equal("origin", local.Head.RemoteName);
            }
            await service.ClearCurrentBranchUpstreamAsync(localPath);
            using (var local = new Repository(localPath))
                Assert.False(local.Head.IsTracking);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task PullRequiresTrackingBranch()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-pull-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "initial\n");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }
            var service = new GitRemoteService();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.PullFastForwardAsync(path));
            Assert.Contains("upstream", error.Message);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task PushRejectsUnbornAndDetachedHead()
    {
        var root = Path.Combine(Path.GetTempPath(), "targit-push-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Repository.Init(root);
            var service = new GitRemoteService();
            await service.AddAsync(root, "origin", Path.Combine(root, "nonexistent.git"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.PushCurrentBranchAsync(root, "origin"));

            using (var repository = new Repository(root))
            {
                File.WriteAllText(Path.Combine(root, "readme.txt"), "initial\n");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                var commit = repository.Commit("Initial", signature, signature);
                Commands.Checkout(repository, commit);
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.PushCurrentBranchAsync(root, "origin"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task PushCurrentBranchToBareRemote()
    {
        var root = Path.Combine(Path.GetTempPath(), "targit-push-" + Guid.NewGuid().ToString("N"));
        var localPath = Path.Combine(root, "local");
        var remotePath = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(localPath);
        Directory.CreateDirectory(remotePath);
        try
        {
            Repository.Init(localPath);
            Repository.Init(remotePath, isBare: true);
            string branchName;
            string commitSha;
            using (var local = new Repository(localPath))
            {
                File.WriteAllText(Path.Combine(localPath, "readme.txt"), "initial\n");
                Commands.Stage(local, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                var commit = local.Commit("Initial", signature, signature);
                commitSha = commit.Sha;
                branchName = local.Head.FriendlyName;
            }
            var service = new GitRemoteService();
            await service.AddAsync(localPath, "origin", remotePath);
            Assert.Equal(branchName, await service.PushCurrentBranchAsync(localPath, "origin"));
            using var remote = new Repository(remotePath);
            Assert.Equal(commitSha, remote.Branches[branchName]?.Tip?.Sha);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(root, true);
        }
    }

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
    public async Task FetchMissingRemoteFailsWithoutChangingRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-fetch-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitRemoteService();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.FetchAsync(path, "missing"));
            Assert.Empty(await service.ListAsync(path));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task CancelledFetchDoesNotStartNetworkOperation()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-fetch-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitRemoteService();
            await service.AddAsync(path, "origin", "https://example.invalid/repo.git");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.FetchAsync(path, "origin", cancellation.Token));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
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
