using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class RebaseEditTests
{
    [Fact]
    public async Task EditStepAmendsReplayedCommitWithoutChangingTarget()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string source;
            string sha;
            string targetSha;
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "root.txt"), "root");
                Commands.Stage(repo, "root.txt");
                repo.Commit("root", sig, sig);
                source = repo.Head.FriendlyName;
                repo.CreateBranch("target");
                File.WriteAllText(Path.Combine(path, "edit.txt"), "before");
                Commands.Stage(repo, "edit.txt");
                sha = repo.Commit("editable", sig, sig).Sha;
                Commands.Checkout(repo, repo.Branches["target"]);
                File.WriteAllText(Path.Combine(path, "target.txt"), "target");
                Commands.Stage(repo, "target.txt");
                targetSha = repo.Commit("target", sig, sig).Sha;
                Commands.Checkout(repo, repo.Branches[source]);
            }

            var plan = new[] { new GitRebaseStep(sha, "editable", GitRebaseAction.Edit) };
            var edits = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [sha] = new Dictionary<string, string> { ["edit.txt"] = "after" },
            };
            await new GitLinearRebaseService().RebaseWithEditsAsync(path, "target", plan, null, edits);
            using var check = new Repository(path);
            Assert.Equal(source, check.Head.FriendlyName);
            Assert.Equal(targetSha, check.Head.Tip.Parents.Single().Sha);
            Assert.Equal(targetSha, check.Branches["target"].Tip.Sha);
            Assert.Equal("after", File.ReadAllText(Path.Combine(path, "edit.txt")));
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
