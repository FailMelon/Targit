using Targit.Application;
using Targit.Core;
using Targit.Infrastructure;
using Xunit;
namespace Targit.Core.Tests;
public sealed class WorkspaceTests
{
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
    public async Task SettingsRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "targit-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(root);
            var settings = new TargitSettings([new RepositoryInfo("/example", "Example")], "/example", ["History", "Changes"], "Changes");
            await store.SaveAsync(settings);
            Assert.Equal(settings, (await store.LoadAsync()) with { Repositories = settings.Repositories });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
