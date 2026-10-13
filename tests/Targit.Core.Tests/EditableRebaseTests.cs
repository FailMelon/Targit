using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class EditableRebaseTests
{
    [Theory]
    [InlineData(GitRebaseAction.Reword)]
    [InlineData(GitRebaseAction.Squash)]
    [InlineData(GitRebaseAction.Fixup)]
    public async Task RebaseExecutesSelectedAction(GitRebaseAction action)
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-edit-rebase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string firstSha;
            string secondSha;
            string targetSha;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "base.txt"), "base");
                Commands.Stage(repo, "base.txt");
                repo.Commit("root", sig, sig);
                var sourceName = repo.Head.FriendlyName;
                repo.CreateBranch("target");
                File.WriteAllText(Path.Combine(path, "one.txt"), "one");
                Commands.Stage(repo, "one.txt");
                firstSha = repo.Commit("first", sig, sig).Sha;
                File.WriteAllText(Path.Combine(path, "two.txt"), "two");
                Commands.Stage(repo, "two.txt");
                secondSha = repo.Commit("second", sig, sig).Sha;
                Commands.Checkout(repo, repo.Branches["target"]);
                File.WriteAllText(Path.Combine(path, "target.txt"), "target");
                Commands.Stage(repo, "target.txt");
                targetSha = repo.Commit("target", sig, sig).Sha;
                Commands.Checkout(repo, repo.Branches[sourceName]);
            }
            var steps = new[]
            {
                new GitRebaseStep(firstSha, "first", GitRebaseAction.Pick),
                new GitRebaseStep(secondSha, "second", action)
            };
            var messages = new Dictionary<string, string> { [secondSha] = "custom message" };
            await new GitLinearRebaseService().RebaseWithMessagesAsync(path, "target", steps, messages);
            using var check = new Repository(path);
            Assert.True(File.Exists(Path.Combine(path, "one.txt")));
            Assert.True(File.Exists(Path.Combine(path, "two.txt")));
            Assert.True(File.Exists(Path.Combine(path, "target.txt")));
            Assert.Equal(targetSha, check.Branches["target"].Tip.Sha);
            if (action == GitRebaseAction.Reword)
            {
                Assert.Equal("custom message", check.Head.Tip.Message);
                Assert.Equal("first", check.Head.Tip.Parents.Single().MessageShort);
            }
            else
            {
                Assert.Equal(targetSha, check.Head.Tip.Parents.Single().Sha);
                Assert.Equal(action == GitRebaseAction.Squash ? "first\n\nsecond" : "first",
                    check.Head.Tip.Message.Trim());
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
