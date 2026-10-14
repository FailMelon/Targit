using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitRemoteTagAcceptanceTests
{
    [Fact]
    public async Task PushAndDeleteRemoteTagWithoutDeletingLocalTag()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitllama-remote-tag-" + Guid.NewGuid().ToString("N"));
        var local = Path.Combine(root, "local");
        var remote = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(local);
        try
        {
            Repository.Init(local);
            Repository.Init(remote, isBare: true);
            using (var repo = new Repository(local))
            {
                File.WriteAllText(Path.Combine(local, "tracked.txt"), "content");
                Commands.Stage(repo, "tracked.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repo.Commit("initial", signature, signature);
                repo.Network.Remotes.Add("origin", remote);
            }

            var service = new GitTagService();
            await service.CreateAsync(local, "v1.0.0");
            await service.PushAsync(local, "origin", "v1.0.0");
            using (var remoteRepo = new Repository(remote))
            {
                Assert.NotNull(remoteRepo.Tags["v1.0.0"]);
            }

            await service.DeleteRemoteAsync(local, "origin", "v1.0.0");
            using (var remoteRepo = new Repository(remote))
            {
                Assert.Null(remoteRepo.Tags["v1.0.0"]);
            }

            Assert.Single(await service.ListAsync(local));
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
