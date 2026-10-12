using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class Phase34Tests
{
    private static string NewPath() { var path = Path.Combine(Path.GetTempPath(), "targit-phase-34-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void Cleanup(string path) { foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly); Directory.Delete(path, true); }
    [Fact] public async Task PlanContainsCommitsAfterUpstream()
    {
        var path = NewPath();
        try
        {
            Repository.Init(path);
            string first;
            using (var r = new Repository(path))
            {
                var s = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "one");
                Commands.Stage(r, "file.txt");
                first = r.Commit("first", s, s).Sha;
                File.WriteAllText(Path.Combine(path, "file.txt"), "two");
                Commands.Stage(r, "file.txt");
                r.Commit("second", s, s);
            }
            var steps = await new GitRebasePlanService().PlanAsync(path, first);
            Assert.Equal("second", Assert.Single(steps).Subject);
            GitRebasePlanService.ValidatePlan(steps);
        }
        finally { Cleanup(path); }
    }
}
