using Targit.Application;
using Targit.Core;
using Xunit;

namespace Targit.Core.Tests;

public sealed class RepositorySessionTests
{
    private sealed class FakeReader : IRepositoryReader
    {
        public bool IsRepository(string path) => true;
        public Task<RepositorySnapshot> ReadAsync(string path, CancellationToken ct = default) =>
            Task.FromResult(new RepositorySnapshot(path, "main", "123", false));
    }

    private sealed class FakeOverview : IRepositoryOverviewReader
    {
        public int Calls { get; private set; }
        public Task<RepositoryOverview> ReadOverviewAsync(string path, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new RepositoryOverview([], [], []));
        }
    }

    [Fact]
    public async Task SameRepositoryUsesSameSession()
    {
        var overview = new FakeOverview();
        using var manager = new RepositorySessionManager(new FakeReader(), overview);
        var first = manager.GetSession(".");
        var second = manager.GetSession(Path.GetFullPath("."));
        Assert.Same(first, second);
        Assert.Equal("main", (await first.OpenAsync()).Branch);
        await first.RefreshAsync();
        Assert.Equal(1, overview.Calls);
        Assert.NotNull(first.LastOverview);
    }
}
