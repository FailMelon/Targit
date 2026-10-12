using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class LibGitIntegrationTests
{
    [Fact]
    public async Task NewlyInitializedRepositoryCanBeOpened()
    {
        var directory = Path.Combine(Path.GetTempPath(), "targit-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Repository.Init(directory);
            var reader = new LibGitRepositoryReader();
            Assert.True(reader.IsRepository(directory));
            var snapshot = await reader.ReadAsync(directory);
            Assert.False(snapshot.IsBare);
            Assert.Equal(Path.GetFullPath(directory), snapshot.Path);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task OverviewReportsWorkingTreeChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), "targit-overview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Repository.Init(directory);
            File.WriteAllText(Path.Combine(directory, "new-file.txt"), "test");
            var reader = new LibGitOverviewReader();
            var overview = await reader.ReadOverviewAsync(directory);
            Assert.Contains(overview.Changes, change => change.Path == "new-file.txt");
        }
        finally { Directory.Delete(directory, true); }
    }
}
