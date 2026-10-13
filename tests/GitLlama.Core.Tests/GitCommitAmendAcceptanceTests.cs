using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitCommitAmendAcceptanceTests
{
    [Fact]
    public async Task AmendsOnlyMessageAndBlocksRemoteTrackedCommits()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-amend-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string originalSha;
            using (var repo = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "file.txt"), "content");
                Commands.Stage(repo, "file.txt");
                var signature = new Signature("Tester", "test@example.invalid", DateTimeOffset.UtcNow);
                originalSha = repo.Commit("old message", signature, signature).Sha;
            }
            var service = new GitCommitAmendService();
            var amendedSha = await service.AmendLatestMessageAsync(path, "new message");
            Assert.NotEqual(originalSha, amendedSha);
            using (var repo = new Repository(path))
            {
                Assert.Equal("new message", repo.Head.Tip!.MessageShort);
                Assert.Equal("content", File.ReadAllText(Path.Combine(path, "file.txt")));
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AmendLatestMessageAsync(path, "must fail"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
