using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class LinearRebaseTests
{
    [Fact]
    public async Task RebaseReplaysLinearCommitsAndKeepsTarget()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-rebase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string sourceName;
            string sourceOriginal;
            string targetOriginal;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "base.txt"), "base");
                Commands.Stage(repo, "base.txt");
                repo.Commit("root", sig, sig);
                sourceName = repo.Head.FriendlyName;
                repo.CreateBranch("target");
                File.WriteAllText(Path.Combine(path, "source.txt"), "source");
                Commands.Stage(repo, "source.txt");
                sourceOriginal = repo.Commit("source", sig, sig).Sha;
                Commands.Checkout(repo, repo.Branches["target"]);
                File.WriteAllText(Path.Combine(path, "target.txt"), "target");
                Commands.Stage(repo, "target.txt");
                targetOriginal = repo.Commit("target", sig, sig).Sha;
                Commands.Checkout(repo, repo.Branches[sourceName]);
            }

            var rewritten = await new GitLinearRebaseService().RebaseOntoAsync(path, "target");
            using var check = new Repository(path);
            Assert.Equal(sourceName, check.Head.FriendlyName);
            Assert.Equal(rewritten, check.Head.Tip.Sha);
            Assert.NotEqual(sourceOriginal, rewritten);
            Assert.Equal(targetOriginal, check.Head.Tip.Parents.Single().Sha);
            Assert.Equal(targetOriginal, check.Branches["target"].Tip.Sha);
            Assert.True(File.Exists(Path.Combine(path, "source.txt")));
            Assert.True(File.Exists(Path.Combine(path, "target.txt")));
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
