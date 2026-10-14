using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phase33Tests
{
    private static string NewPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-phase-33-" + Guid.NewGuid().ToString("N"));
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
    public async Task SoftResetMovesHeadWithoutDeletingWorkingFile()
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

            await new GitResetService().ResetAsync(path, first, ResetMode.Soft);
            using var check = new Repository(path);
            Assert.Equal(first, check.Head.Tip.Sha);
            Assert.Equal("two", File.ReadAllText(Path.Combine(path, "file.txt")));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Theory]
    [InlineData(ResetMode.Soft, "two", true)]
    [InlineData(ResetMode.Mixed, "two", true)]
    [InlineData(ResetMode.Hard, "one", false)]
    public async Task ResetModesPreserveOrRestoreFiles(ResetMode mode, string expectedFile, bool expectedDirty)
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

            await new GitResetService().ResetAsync(path, first, mode);
            using var check = new Repository(path);
            Assert.Equal(first, check.Head.Tip!.Sha);
            Assert.Equal(expectedFile, File.ReadAllText(Path.Combine(path, "file.txt")));
            Assert.Equal(expectedDirty, check.RetrieveStatus().IsDirty);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task RejectsDirtyTreeAndUnrelatedCommit()
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
                r.CreateBranch("other");
                File.WriteAllText(Path.Combine(path, "file.txt"), "two");
                Commands.Stage(r, "file.txt");
                r.Commit("second", s, s);
                Commands.Checkout(r, r.Branches["other"]);
                File.WriteAllText(Path.Combine(path, "unrelated.txt"), "other");
                Commands.Stage(r, "unrelated.txt");
                r.Commit("other commit", s, s);
            }

            var service = new GitResetService();
            using (var r = new Repository(path))
            {
                var otherSha = r.Head.Tip!.Sha;
                Commands.Checkout(r, r.Branches.First(branch => branch.FriendlyName != "other" && !branch.IsRemote));
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    service.ResetAsync(path, otherSha, ResetMode.Soft)
                );
            }

            File.WriteAllText(Path.Combine(path, "file.txt"), "dirty");
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetAsync(path, first, ResetMode.Hard));
            Assert.Equal("dirty", File.ReadAllText(Path.Combine(path, "file.txt")));
        }
        finally
        {
            Cleanup(path);
        }
    }
}
