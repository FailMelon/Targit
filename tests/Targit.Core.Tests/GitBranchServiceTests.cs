using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitBranchServiceTests
{
    [Fact]
    public async Task CreatesAndChecksOutLocalBranch()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-branch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "initial\n");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }
            var service = new GitBranchService();
            Assert.Equal("feature/test", await service.CreateAsync(path, "feature/test"));
            Assert.Equal("feature/test", await service.CheckoutAsync(path, "feature/test"));
            using var result = new Repository(path);
            Assert.Equal("feature/test", result.Head.FriendlyName);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(path, "feature/test"));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Theory]
    [InlineData("bad name")]
    [InlineData("bad..name")]
    [InlineData("bad.lock")]
    [InlineData("-invalid")]
    public async Task RejectsInvalidBranchNames(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-branch-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                File.WriteAllText(Path.Combine(path, "readme.txt"), "initial\n");
                Commands.Stage(repository, "readme.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }
            await Assert.ThrowsAsync<ArgumentException>(() => new GitBranchService().CreateAsync(path, name));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
