using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class FileActionTests
{
    [Fact]
    public void SuggestsTheFileItsExtensionAndItsFolder()
    {
        Assert.Equal(
            [
                ("Ignore this file", "/Build/Output/app.log"),
                ("Ignore all .log files", "*.log"),
                ("Ignore folder Build/Output/", "/Build/Output/"),
            ],
            GitIgnoreService.Suggestions("Build/Output/app.log")
        );
        Assert.Equal([("Ignore this file", "/.env")], GitIgnoreService.Suggestions(".env"));
    }

    [Fact]
    public async Task AddsEachIgnorePatternOnce()
    {
        var path = NewRepository();
        try
        {
            File.WriteAllText(Path.Combine(path, ".gitignore"), "bin/");
            var service = new GitIgnoreService();

            Assert.True(await service.AddPatternAsync(path, "*.log"));
            Assert.False(await service.AddPatternAsync(path, "*.log"));
            Assert.Equal("bin/\n*.log\n", File.ReadAllText(Path.Combine(path, ".gitignore")));
            File.WriteAllText(Path.Combine(path, "debug.log"), "noise");
            using var repository = new Repository(path);
            Assert.True(repository.Ignore.IsPathIgnored("debug.log"));
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public async Task StashesOneFileAndLeavesTheOthers()
    {
        var path = NewRepository();
        try
        {
            File.WriteAllText(Path.Combine(path, "a.txt"), "changed a");
            File.WriteAllText(Path.Combine(path, "b.txt"), "changed b");
            File.WriteAllText(Path.Combine(path, "new.txt"), "new");
            var service = new GitStashService();

            await service.SaveFileAsync(path, "a.txt", "Test", "test@example.invalid");
            await service.SaveFileAsync(path, "new.txt", "Test", "test@example.invalid");

            Assert.Equal("a", File.ReadAllText(Path.Combine(path, "a.txt")));
            Assert.Equal("changed b", File.ReadAllText(Path.Combine(path, "b.txt")));
            Assert.False(File.Exists(Path.Combine(path, "new.txt")));
            var stashes = await service.ListAsync(path);
            Assert.Equal(
                [": new.txt", ": a.txt"],
                stashes.Select(stash => stash.Message[stash.Message.LastIndexOf(':')..])
            );
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveFileAsync(path, "a.txt", "Test", "test@example.invalid")
            );
        }
        finally
        {
            Delete(path);
        }
    }

    private static string NewRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-file-actions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Repository.Init(path);
        using var repository = new Repository(path);
        File.WriteAllText(Path.Combine(path, "a.txt"), "a");
        File.WriteAllText(Path.Combine(path, "b.txt"), "b");
        Commands.Stage(repository, "*");
        var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
        repository.Commit("Initial", signature, signature);
        return path;
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
