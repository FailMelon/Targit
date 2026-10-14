using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class ThreeWayConflictTests
{
    [Fact]
    public async Task ConflictedMergeExposesAllThreeStages()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-three-way-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                var file = Path.Combine(path, "conflict.txt");
                File.WriteAllText(file, "base\n");
                Commands.Stage(repo, "conflict.txt");
                repo.Commit("root", sig, sig);
                var source = repo.Head.FriendlyName;
                repo.CreateBranch("other");
                File.WriteAllText(file, "ours\n");
                Commands.Stage(repo, "conflict.txt");
                repo.Commit("ours", sig, sig);
                Commands.Checkout(repo, repo.Branches["other"]);
                File.WriteAllText(file, "theirs\n");
                Commands.Stage(repo, "conflict.txt");
                repo.Commit("theirs", sig, sig);
                Commands.Checkout(repo, repo.Branches[source]);
            }

            var status = await new GitMergeService().MergeAsync(path, "other");
            Assert.Equal(MergeStatus.Conflicts, status);
            var stage = await new GitThreeWayConflictService().InspectAsync(path, "conflict.txt");
            Assert.Equal("base\n", stage.Base);
            Assert.Equal("ours\n", stage.Ours);
            Assert.Equal("theirs\n", stage.Theirs);
            File.WriteAllText(Path.Combine(path, "conflict.txt"), "resolved\n");
            await new GitConflictResolutionService().ResolveUsingWorkingTreeAsync(path, "conflict.txt");
            Assert.Empty(await new GitConflictDetectionService().ListAsync(path));
            var sha = await new GitMergeLifecycleService().ContinueAsync(path, "merge resolved");
            using var check = new Repository(path);
            Assert.Equal(2, check.Lookup<Commit>(sha)!.Parents.Count());
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
