using GitLlama.Application;
using GitLlama.Core;
using GitLlama.Infrastructure;
using Xunit;
namespace GitLlama.Core.Tests;
public sealed class WorkspaceTests
{
    [Fact]
    public void RenamedAppImportsLegacySettingsWithoutOverwritingOrDeletingThem()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitllama-migration-" + Guid.NewGuid().ToString("N"));
        var legacy = Path.Combine(root, "Targit"); var current = Path.Combine(root, "GitLlama");
        Directory.CreateDirectory(legacy); Directory.CreateDirectory(current);
        try
        {
            File.WriteAllText(Path.Combine(legacy, "settings.json"), "legacy preferences");
            File.WriteAllText(Path.Combine(legacy, "workspace.json"), "legacy layout");
            File.WriteAllText(Path.Combine(current, "settings.json"), "existing GitLlama preferences");
            Assert.Equal(current, AppDataPaths.SettingsDirectory(root));
            Assert.Equal("existing GitLlama preferences", File.ReadAllText(Path.Combine(current, "settings.json")));
            Assert.Equal("legacy layout", File.ReadAllText(Path.Combine(current, "workspace.json")));
            Assert.Equal("legacy preferences", File.ReadAllText(Path.Combine(legacy, "settings.json")));
            File.Delete(Path.Combine(current, "workspace.json"));
            AppDataPaths.SettingsDirectory(root);
            Assert.False(File.Exists(Path.Combine(current, "workspace.json")));
            Assert.True(File.Exists(Path.Combine(legacy, "workspace.json")));
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class FakeReader : IRepositoryReader
    {
        public bool IsRepository(string path) => true;
        public Task<RepositorySnapshot> ReadAsync(string path, CancellationToken ct = default) => Task.FromResult(new RepositorySnapshot(path, "main", null, false));
    }
    [Fact]
    public async Task OpenNormalizesPath()
    {
        var workspace = new RepositoryWorkspace(new FakeReader());
        var snapshot = await workspace.OpenAsync(".");
        Assert.Equal(Path.GetFullPath("."), snapshot.Path);
    }
    [Fact]
    public async Task MissingSettingsReturnSafeDefaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitllama-empty-" + Guid.NewGuid().ToString("N"));
        var loaded = await new JsonSettingsStore(root).LoadAsync();
        Assert.Empty(loaded.Repositories);
        Assert.Null(loaded.ActiveRepository);
        Assert.Equal("Standard", loaded.LayoutName);
    }

    [Fact]
    public async Task CorruptSettingsAreBackedUpAndDefaultsLoaded()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitllama-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "settings.json"), "{ invalid json");
            var store = new JsonSettingsStore(root);
            var loaded = await store.LoadAsync();
            Assert.Empty(loaded.Repositories);
            Assert.Null(loaded.ActiveRepository);
            Assert.Single(Directory.GetFiles(root, "settings.json.corrupt-*"));
            await store.SaveAsync(new GitLlamaSettings([], null));
            Assert.True(File.Exists(Path.Combine(root, "settings.json")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SettingsRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitllama-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(root);
            var settings = new GitLlamaSettings([new RepositoryInfo("/example", "Example")], "/example", ["History", "Changes"], "Changes", LayoutName: "Review");
            await store.SaveAsync(settings);
            var loaded = await store.LoadAsync();
            Assert.Equal(settings.ActiveRepository, loaded.ActiveRepository);
            Assert.Equal(settings.ActiveTab, loaded.ActiveTab);
            Assert.Equal(settings.LayoutName, loaded.LayoutName);
            Assert.Equal(settings.LeftPanel, loaded.LeftPanel);
            Assert.Equal(settings.RightPanel, loaded.RightPanel);
            Assert.Equal(settings.Repositories, loaded.Repositories);
            Assert.Equal(settings.Tabs, loaded.Tabs);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
