using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitLocalIdentityAcceptanceTests
{
    [Fact]
    public async Task SavesLocalIdentityWithoutChangingGlobalConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-local-identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitIdentityService();
            await service.SetLocalAsync(path, " Local Tester ", " local@example.invalid ");
            var identity = await service.ReadAsync(path);
            Assert.Equal("Local Tester", identity.Name);
            Assert.Equal("local@example.invalid", identity.Email);
            using (var repo = new Repository(path))
            {
                Assert.Equal("Local Tester", repo.Config.Get<string>("user.name", ConfigurationLevel.Local)?.Value);
                Assert.Equal("local@example.invalid", repo.Config.Get<string>("user.email", ConfigurationLevel.Local)?.Value);
            }
            await Assert.ThrowsAnyAsync<ArgumentException>(() => service.SetLocalAsync(path, "", "test@example.invalid"));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => service.SetLocalAsync(path, "Test", ""));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}
