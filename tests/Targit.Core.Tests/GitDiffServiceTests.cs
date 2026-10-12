using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitDiffServiceTests
{
    [Fact]
    public async Task ReportsStagedAndUnstagedVersionsSeparately()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var file = Path.Combine(path, "sample.txt");
            File.WriteAllText(file, "base\n");
            using (var repository = new Repository(path))
            {
                Commands.Stage(repository, "sample.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Base", signature, signature);
            }

            File.WriteAllText(file, "staged\n");
            using (var repository = new Repository(path))
                Commands.Stage(repository, "sample.txt");
            File.WriteAllText(file, "unstaged\n");

            var result = await new GitDiffService().GetFileDiffAsync(path, "sample.txt");
            Assert.Contains("+staged", result.StagedPatch);
            Assert.DoesNotContain("+unstaged", result.StagedPatch);
            Assert.Contains("+unstaged", result.UnstagedPatch);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
