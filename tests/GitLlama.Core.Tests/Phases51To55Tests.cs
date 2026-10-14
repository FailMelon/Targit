using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phases51To55Tests
{
    [Fact]
    public async Task UnifiedHistoryReportsRootAndModifiedCommitsAndGraphLanes()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-51-55-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string first;
            string second;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("History Tester", "history@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "a.txt"), "one\ntwo\n");
                Commands.Stage(repo, "a.txt");
                first = repo.Commit("root", sig, sig).Sha;
                File.WriteAllText(Path.Combine(path, "a.txt"), "one\nthree\n");
                Commands.Stage(repo, "a.txt");
                second = repo.Commit("update", sig, sig).Sha;
                repo.Tags.Add("v1", repo.Head.Tip);
            }

            var service = new GitHistoryWorkspaceService();
            var root = await service.InspectAsync(path, first);
            Assert.Empty(root.Parents);
            Assert.Equal(2, Assert.Single(root.Files).Added);
            var update = await service.InspectAsync(path, second);
            Assert.Equal(first, Assert.Single(update.Parents));
            Assert.Equal("History Tester", update.Author);
            Assert.Contains("tag:v1", update.Labels);
            var file = Assert.Single(update.Files);
            Assert.Equal("a.txt", file.Path);
            Assert.Equal(1, file.Added);
            Assert.Equal(1, file.Deleted);
            var lanes = await new GitGraphLaneService().GetAsync(path);
            Assert.Contains(lanes, l => l.Sha == second && l.Parents.Contains(first));
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, " "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, new string('0', 40)));
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
