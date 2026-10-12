using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class LibGitIntegrationTests
{
    private static void DeleteTemporaryRepository(string directory)
    {
        if (!Directory.Exists(directory)) return;
        // Git object files are read-only on Windows. Clear the flag before cleanup.
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
        }
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task OverviewIncludesCommittedHistoryAndBranch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "targit-commit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Repository.Init(directory);
            using (var repo = new Repository(directory))
            {
                File.WriteAllText(Path.Combine(directory, "readme.txt"), "hello");
                Commands.Stage(repo, "readme.txt");
                var author = new Signature("Targit Tests", "tests@example.invalid", DateTimeOffset.UtcNow);
                repo.Commit("Initial commit", author, author);
            }
            var overview = await new LibGitOverviewReader().ReadOverviewAsync(directory);
            Assert.Contains(overview.Commits, commit => commit.Message == "Initial commit");
            Assert.NotEmpty(overview.Branches);
            Assert.Empty(overview.Changes);
        }
        finally { DeleteTemporaryRepository(directory); }
    }

    [Fact]
    public async Task BareRepositoryOverviewHasNoWorkingTreeChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), "targit-bare-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Repository.Init(directory, isBare: true);
            var overview = await new LibGitOverviewReader().ReadOverviewAsync(directory);
            Assert.Empty(overview.Changes);
        }
        finally { DeleteTemporaryRepository(directory); }
    }

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
        finally { DeleteTemporaryRepository(directory); }
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
        finally { DeleteTemporaryRepository(directory); }
    }
}
