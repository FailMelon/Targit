using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitIdentityServiceTests
{
    [Fact]
    public async Task ReadsLocalCommitIdentity()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repository = new Repository(path))
            {
                repository.Config.Set("user.name", "GitLlama Test");
                repository.Config.Set("user.email", "test@example.invalid");
            }

            var identity = await new GitIdentityService().ReadAsync(path);
            Assert.Equal("GitLlama Test", identity.Name);
            Assert.Equal("test@example.invalid", identity.Email);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task SetsRepositoryLocalIdentity()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-local-identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            var service = new GitIdentityService();
            await service.SetLocalAsync(path, "Local Author", "local@example.invalid");
            var identity = await service.ReadAsync(path);
            Assert.Equal("Local Author", identity.Name);
            Assert.Equal("local@example.invalid", identity.Email);
            using var repository = new Repository(path);
            Assert.Equal("Local Author", repository.Config.Get<string>("user.name", ConfigurationLevel.Local)?.Value);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }
}
