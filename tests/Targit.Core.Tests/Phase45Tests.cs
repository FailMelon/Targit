using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class Phase45Tests
{
    [Fact]
    public async Task ClassifiesIdenticalAncestorAndDivergedCommits()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-45-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string first;
            string second;
            string branch;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "first");
                Commands.Stage(repo, "file.txt");
                first = repo.Commit("first", sig, sig).Sha;
                repo.CreateBranch("side");
                File.WriteAllText(Path.Combine(path, "file.txt"), "second");
                Commands.Stage(repo, "file.txt");
                second = repo.Commit("second", sig, sig).Sha;
                Commands.Checkout(repo, repo.Branches["side"]);
                File.WriteAllText(Path.Combine(path, "other.txt"), "side");
                Commands.Stage(repo, "other.txt");
                branch = repo.Commit("side", sig, sig).Sha;
            }
            var service = new GitCommitRelationshipService();
            Assert.Equal(GitCommitRelationship.Same, (await service.InspectAsync(path, first, first)).Relationship);
            Assert.Equal(GitCommitRelationship.FirstIsAncestor, (await service.InspectAsync(path, first, second)).Relationship);
            Assert.Equal(GitCommitRelationship.SecondIsAncestor, (await service.InspectAsync(path, second, first)).Relationship);
            var diverged = await service.InspectAsync(path, second, branch);
            Assert.Equal(GitCommitRelationship.Diverged, diverged.Relationship);
            Assert.Equal(first, diverged.MergeBaseSha);
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, " ", second));
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, first, new string('0', 40)));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}