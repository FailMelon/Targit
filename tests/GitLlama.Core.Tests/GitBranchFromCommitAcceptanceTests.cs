using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitBranchFromCommitAcceptanceTests
{
    [Fact]
    public async Task CreatesBranchAtOlderCommitWithoutChangingHeadOrFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-branch-from-commit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string oldSha;
            string headSha;
            using (var repo = new Repository(path))
            {
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "old");
                Commands.Stage(repo, "file.txt");
                oldSha = repo.Commit("old", signature, signature).Sha;
                File.WriteAllText(Path.Combine(path, "file.txt"), "new");
                Commands.Stage(repo, "file.txt");
                headSha = repo.Commit("new", signature, signature).Sha;
            }

            var service = new GitBranchFromCommitService();
            Assert.Equal("recovery/old-work", await service.CreateAsync(path, oldSha, "recovery/old-work"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync(path, oldSha, "recovery/old-work")
            );
            Assert.Equal("new", File.ReadAllText(Path.Combine(path, "file.txt")));
            using (var repo = new Repository(path))
            {
                Assert.Equal(headSha, repo.Head.Tip!.Sha);
                Assert.Equal(oldSha, repo.Branches["recovery/old-work"]!.Tip!.Sha);
                Assert.False(repo.RetrieveStatus().IsDirty);
            }
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
}
