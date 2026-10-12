using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class Phase35Tests
{
    private static string NewPath() { var path = Path.Combine(Path.GetTempPath(), "targit-phase-35-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void Cleanup(string path) { foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly); Directory.Delete(path, true); }
    [Fact] public async Task CleanRepositoryHasNoRebaseConflicts()
    {
        var path = NewPath();
        try
        {
            Repository.Init(path);
            var state = await new GitRebaseConflictService().InspectAsync(path);
            Assert.False(state.IsRebasing);
            Assert.Empty(state.Paths);
        }
        finally { Cleanup(path); }
    }
    [Fact]
    public async Task RejectsBareRepository()
    {
        var path = NewPath();
        try
        {
            Repository.Init(path, true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new GitRebaseConflictService().InspectAsync(path));
        }
        finally { Cleanup(path); }
    }

}
