using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class SubmoduleServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "GitLlama-Submodules-" + Guid.NewGuid().ToString("N")
    );
    private readonly string _parent;
    private readonly string _library;

    public SubmoduleServiceTests()
    {
        _parent = CreateRepository("Game", "README.md");
        _library = CreateRepository("UnrealSharp", "Plugin.cs");
    }

    [Theory]
    [InlineData("https://github.com/owner/UnrealSharp.git", "UnrealSharp")]
    [InlineData("git@github.com:owner/UnrealSharp.git", "UnrealSharp")]
    [InlineData("https://dev.azure.com/org/project/_git/Engine/", "Engine")]
    [InlineData("", "")]
    public void SuggestPathUsesTheRepositoryName(string url, string expected) =>
        Assert.Equal(expected, GitSubmoduleService.SuggestPath(url));

    [Fact]
    public async Task AddsAndRemovesASubmodule()
    {
        var service = new GitSubmoduleService();
        using (AllowLocalSubmoduleClones())
        {
            await service.AddAsync(_parent, _library, "libs/UnrealSharp");
        }

        var module = Assert.Single(await service.ListAsync(_parent));
        Assert.Equal("libs/UnrealSharp", module.Path);
        Assert.True(module.IsCheckedOut);
        Assert.True(File.Exists(Path.Combine(_parent, "libs", "UnrealSharp", "Plugin.cs")));
        using (var repo = new Repository(_parent))
        {
            Assert.Equal(FileStatus.NewInIndex, repo.RetrieveStatus(".gitmodules"));
            Commands.Stage(repo, ".gitmodules");
            var signature = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
            repo.Commit("Add UnrealSharp", signature, signature);
        }

        await service.RemoveAsync(_parent, module.Name);

        Assert.Empty(await service.ListAsync(_parent));
        Assert.False(Directory.Exists(Path.Combine(_parent, "libs", "UnrealSharp")));
        // The clone's history is kept so nothing unpushed is lost.
        Assert.True(Directory.Exists(Path.Combine(_parent, ".git", "modules", module.Name)));
    }

    [Fact]
    public async Task AddRefusesAFolderThatAlreadyHasFiles()
    {
        Directory.CreateDirectory(Path.Combine(_parent, "libs", "UnrealSharp"));
        File.WriteAllText(Path.Combine(_parent, "libs", "UnrealSharp", "notes.txt"), "keep me");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GitSubmoduleService().AddAsync(_parent, _library, "libs/UnrealSharp")
        );
        Assert.Contains("doesn't exist yet or is empty", error.Message);
    }

    [Fact]
    public async Task AddRefusesPathsOutsideTheRepository()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new GitSubmoduleService().AddAsync(_parent, _library, "../outside")
        );
    }

    [Fact]
    public async Task RemoveRefusesASubmoduleWithUncommittedChanges()
    {
        var service = new GitSubmoduleService();
        using (AllowLocalSubmoduleClones())
        {
            await service.AddAsync(_parent, _library, "libs/UnrealSharp");
        }

        File.AppendAllText(Path.Combine(_parent, "libs", "UnrealSharp", "Plugin.cs"), "// edit\n");
        var module = Assert.Single(await service.ListAsync(_parent));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RemoveAsync(_parent, module.Name)
        );
        Assert.Contains("Commit or stash changes inside the submodule", error.Message);
        Assert.True(File.Exists(Path.Combine(_parent, "libs", "UnrealSharp", "Plugin.cs")));
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, true);
    }

    private string CreateRepository(string name, string file)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        Repository.Init(path);
        using var repo = new Repository(path);
        repo.Config.Set("user.name", "Test");
        repo.Config.Set("user.email", "test@example.com");
        File.WriteAllText(Path.Combine(path, file), "// " + name + "\n");
        Commands.Stage(repo, file);
        var signature = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
        repo.Commit("Initial commit", signature, signature);
        return path;
    }

    // Git blocks cloning submodules from local paths by default; these tests use local repositories.
    private static IDisposable AllowLocalSubmoduleClones()
    {
        var names = new[] { "GIT_CONFIG_COUNT", "GIT_CONFIG_KEY_0", "GIT_CONFIG_VALUE_0" };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", "1");
        Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", "protocol.file.allow");
        Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", "always");
        return new Restore(() =>
        {
            foreach (var (name, value) in previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        });
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
