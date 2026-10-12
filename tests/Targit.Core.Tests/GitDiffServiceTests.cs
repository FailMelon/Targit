using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitDiffServiceTests
{
    [Fact]
    public async Task DeletedTrackedFileAppearsInUnstagedPatch()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-deleted-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var file = Path.Combine(path, "removed.txt");
            File.WriteAllText(file, "removed content\n");
            using (var repository = new Repository(path))
            {
                Commands.Stage(repository, "removed.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
            }
            File.Delete(file);
            var result = await new GitDiffService().GetFileDiffAsync(path, "removed.txt");
            Assert.Empty(result.StagedPatch);
            Assert.Contains("-removed content", result.UnstagedPatch);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task UntrackedFileIsVisibleAsAddedLines()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-untracked-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            File.WriteAllText(Path.Combine(path, "new.txt"), "first line\\nsecond line\\n");
            var result = await new GitDiffService().GetFileDiffAsync(path, "new.txt");
            Assert.Empty(result.StagedPatch);
            Assert.Contains("+first line", result.UnstagedPatch);
            Assert.Contains("+second line", result.UnstagedPatch);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }

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
