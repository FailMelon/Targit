using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitHardResetAcceptanceTests
{
    [Fact]
    public async Task HardResetRestoresTrackedFilesAndIndexAndRejectsDirtyTree()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-hard-reset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string ancestor;
            using (var repo = new Repository(path))
            {
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "base");
                Commands.Stage(repo, "file.txt");
                ancestor = repo.Commit("base", signature, signature).Sha;
                File.WriteAllText(Path.Combine(path, "file.txt"), "next");
                Commands.Stage(repo, "file.txt");
                repo.Commit("next", signature, signature);
            }
            var service = new GitHardResetService();
            File.WriteAllText(Path.Combine(path, "file.txt"), "dirty");
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetToAncestorAsync(path, ancestor));
            Assert.Equal("dirty", File.ReadAllText(Path.Combine(path, "file.txt")));
            File.WriteAllText(Path.Combine(path, "file.txt"), "next");
            Assert.Equal(ancestor, await service.ResetToAncestorAsync(path, ancestor));
            Assert.Equal("base", File.ReadAllText(Path.Combine(path, "file.txt")));
            using (var repo = new Repository(path))
            {
                Assert.Equal(ancestor, repo.Head.Tip!.Sha);
                Assert.False(repo.RetrieveStatus().IsDirty);
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
