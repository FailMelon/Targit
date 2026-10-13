using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitBranchDivergenceTests
{
    [Fact]
    public async Task ReportsZeroDivergenceForFreshClone()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitllama-divergence-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var clone = Path.Combine(root, "clone");
        Directory.CreateDirectory(source);
        try
        {
            Repository.Init(source);
            using (var repository = new Repository(source))
            {
                File.WriteAllText(Path.Combine(source, "readme.txt"), "hello");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }
            Repository.Clone(source, clone);
            var divergence = await new GitBranchDivergenceService().GetCurrentAsync(clone);
            Assert.Equal(0, divergence.Ahead);
            Assert.Equal(0, divergence.Behind);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(root, true);
        }
    }
}
