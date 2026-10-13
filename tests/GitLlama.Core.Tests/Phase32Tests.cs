using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phase32Tests
{
    private static string NewPath() { var path = Path.Combine(Path.GetTempPath(), "gitllama-phase-32-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void Cleanup(string path) { foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly); Directory.Delete(path, true); }
    [Fact] public async Task AbortWithoutMergeIsRejected()
    {
        var path = NewPath();
        try { Repository.Init(path); await Assert.ThrowsAsync<InvalidOperationException>(() => new GitMergeLifecycleService().AbortAsync(path)); }
        finally { Cleanup(path); }
    }
}
