using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phases61To65Tests
{
    [Fact]
    public async Task MergeBranchFastForwardAndConflictInspection()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-merge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string baseBranch;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "base\n");
                Commands.Stage(repo, "file.txt");
                repo.Commit("initial", sig, sig);
                baseBranch = repo.Head.FriendlyName;
                repo.CreateBranch("feature");
                Commands.Checkout(repo, repo.Branches["feature"]);
                File.WriteAllText(Path.Combine(path, "file.txt"), "feature\n");
                Commands.Stage(repo, "file.txt");
                repo.Commit("feature", sig, sig);
                Commands.Checkout(repo, repo.Branches[baseBranch]);
            }
            var result = await new GitMergeService().MergeAsync(path, "feature");
            Assert.Equal(MergeStatus.FastForward, result);
            Assert.Empty(await new GitConflictDetectionService().ListAsync(path));
            Assert.Equal("feature\n", File.ReadAllText(Path.Combine(path, "file.txt")).Replace("\r\n", "\n"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
