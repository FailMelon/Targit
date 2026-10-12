using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class GitIdentityServiceTests
{
    [Fact]
    public async Task ReadsLocalCommitIdentity()
    {
        var path = Path.Combine(Path.GetTempPath(), "targit-identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                repository.Config.Set("user.name", "Targit Test");
                repository.Config.Set("user.email", "test@example.invalid");
            }
            var identity = await new GitIdentityService().ReadAsync(path);
            Assert.Equal("Targit Test", identity.Name);
            Assert.Equal("test@example.invalid", identity.Email);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
