using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phase36Tests
{
    private static string NewPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-phase-36-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        }

        Directory.Delete(path, true);
    }

    [Fact]
    public async Task GraphIncludesCommitParentsAndBranchLabels()
    {
        var path = NewPath();
        try
        {
            Repository.Init(path);
            string first;
            using (var r = new Repository(path))
            {
                var s = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "one");
                Commands.Stage(r, "file.txt");
                first = r.Commit("first", s, s).Sha;
                File.WriteAllText(Path.Combine(path, "file.txt"), "two");
                Commands.Stage(r, "file.txt");
                r.Commit("second", s, s);
            }

            var nodes = await new GitCommitGraphService().GetAsync(path);
            Assert.Equal(2, nodes.Count);
            Assert.Equal(first, Assert.Single(nodes[0].Parents));
            Assert.Contains("master", nodes[0].Labels);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task EmptyRepositoryAndCountValidation()
    {
        var path = NewPath();
        try
        {
            Repository.Init(path);
            var service = new GitCommitGraphService();
            Assert.Empty(await service.GetAsync(path));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetAsync(path, 0));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetAsync(path, 10001));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.GetAsync(path, cancellationToken: cancelled.Token)
            );
        }
        finally
        {
            Cleanup(path);
        }
    }
}
