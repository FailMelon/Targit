using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitStashServiceTests
{
    [Fact]
    public async Task EmptyRepositoryHasNoStashes()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-stashes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            Assert.Empty(await new GitStashService().ListAsync(path));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
