using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class DiscardLineEndingTests
{
    [Theory]
    [InlineData(true, "one\r\ntwo\r\n")]
    [InlineData(false, "one\ntwo\n")]
    public async Task DiscardWritesTheFileWithCheckoutLineEndings(bool autocrlf, string expected)
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-discard-eol-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                repository.Config.Set("core.autocrlf", autocrlf);
                File.WriteAllText(Path.Combine(path, "notes.txt"), "one\ntwo\n");
                Commands.Stage(repository, "notes.txt");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
                // Check the file out again so the working copy has the endings Git would give it.
                File.Delete(Path.Combine(path, "notes.txt"));
                repository.CheckoutPaths(
                    "HEAD",
                    ["notes.txt"],
                    new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force }
                );
            }

            File.WriteAllText(Path.Combine(path, "notes.txt"), "edited\n");
            await new GitWorkingTreeReviewService().DiscardUnstagedAsync(path, "notes.txt", "DISCARD");

            Assert.Equal(expected, File.ReadAllText(Path.Combine(path, "notes.txt")));
            using var check = new Repository(path);
            Assert.Equal(FileStatus.Unaltered, check.RetrieveStatus("notes.txt"));
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public async Task DiscardClearsAFileThatOnlyLooksModified()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-discard-phantom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                repository.Config.Set("core.autocrlf", true);
                File.WriteAllText(Path.Combine(path, "notes.txt"), "one\r\ntwo\r\n");
                File.WriteAllText(Path.Combine(path, "staged.txt"), "one\r\n");
                Commands.Stage(repository, "*");
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("Initial", signature, signature);
                File.WriteAllText(Path.Combine(path, "staged.txt"), "two\r\n");
                Commands.Stage(repository, "staged.txt");
            }

            // Same content with LF endings: Git sees a different size and lists the file as modified.
            File.WriteAllText(Path.Combine(path, "notes.txt"), "one\ntwo\n");
            File.SetLastWriteTimeUtc(Path.Combine(path, "notes.txt"), DateTime.UtcNow.AddMinutes(1));
            await new GitWorkingTreeReviewService().DiscardUnstagedAsync(path, "notes.txt", "DISCARD");

            using var check = new Repository(path);
            Assert.Equal(FileStatus.Unaltered, check.RetrieveStatus("notes.txt"));
            Assert.Equal("one\r\ntwo\r\n", File.ReadAllText(Path.Combine(path, "notes.txt")));
            // Other staged work is left alone.
            Assert.Equal(FileStatus.ModifiedInIndex, check.RetrieveStatus("staged.txt"));
        }
        finally
        {
            Delete(path);
        }
    }

    private static void Delete(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        }

        Directory.Delete(path, true);
    }
}
