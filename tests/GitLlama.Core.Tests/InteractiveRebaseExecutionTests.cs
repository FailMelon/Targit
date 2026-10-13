using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class InteractiveRebaseExecutionTests
{
    [Theory]
    [InlineData(GitRebaseAction.Reword)]
    [InlineData(GitRebaseAction.Squash)]
    [InlineData(GitRebaseAction.Fixup)]
    [InlineData(GitRebaseAction.Drop)]
    public async Task PlannedRebaseExecutesAndPreservesTarget(GitRebaseAction action)
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-interactive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string branch;
            string first;
            string second;
            string target;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                void CommitFile(string file, string message)
                {
                    File.WriteAllText(Path.Combine(path, file), message);
                    Commands.Stage(repo, file);
                    repo.Commit(message, sig, sig);
                }
                CommitFile("root.txt", "root");
                branch = repo.Head.FriendlyName;
                repo.CreateBranch("target");
                CommitFile("one.txt", "one");
                first = repo.Head.Tip.Sha;
                CommitFile("two.txt", "two");
                second = repo.Head.Tip.Sha;
                Commands.Checkout(repo, repo.Branches["target"]);
                CommitFile("target.txt", "target");
                target = repo.Head.Tip.Sha;
                Commands.Checkout(repo, repo.Branches[branch]);
            }
            var plan = new[]
            {
                new GitRebaseStep(first, "one", GitRebaseAction.Pick),
                new GitRebaseStep(second, "two", action)
            };
            var messages = new Dictionary<string, string> { [second] = "replacement message" };
            var result = await new GitLinearRebaseService().RebaseWithMessagesAsync(path, "target", plan, messages);
            using var check = new Repository(path);
            Assert.Equal(branch, check.Head.FriendlyName);
            Assert.Equal(result, check.Head.Tip.Sha);
            Assert.Equal(target, check.Branches["target"].Tip.Sha);
            Assert.True(File.Exists(Path.Combine(path, "one.txt")));
            Assert.Equal(action != GitRebaseAction.Drop, File.Exists(Path.Combine(path, "two.txt")));
            if (action == GitRebaseAction.Reword)
                Assert.Equal("replacement message", check.Head.Tip.Message.Trim());
            if (action == GitRebaseAction.Squash)
                Assert.Contains("two", check.Head.Tip.Message);
            if (action == GitRebaseAction.Fixup)
                Assert.Equal("one", check.Head.Tip.Message.Trim());
            if (action is GitRebaseAction.Squash or GitRebaseAction.Fixup or GitRebaseAction.Drop)
                Assert.Equal(target, check.Head.Tip.Parents.Single().Sha);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
