using System.Security.Cryptography;
using System.Text;
using GitLlama.Infrastructure;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class ProtectedIntegrationStoreTests
{
    [Fact]
    public async Task WindowsCredentialsAreEncryptedProfileScopedAndFailClosedOnCorruption()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "GitLlama-SecureStore-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ProtectedIntegrationStore(root);
            await store.SaveAsync(
                "work",
                new GithubStoredConnection
                {
                    Login = "Work",
                    AccessToken = "access-secret",
                    RefreshToken = "refresh-secret",
                }
            );
            Assert.Null(await store.LoadAsync("personal"));
            Assert.Equal("refresh-secret", (await store.LoadAsync("work"))!.RefreshToken);
            var file = Directory.GetFiles(Path.Combine(root, "connections")).Single();
            var contents = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file));
            Assert.DoesNotContain("access-secret", contents);
            Assert.DoesNotContain("refresh-secret", contents);
            await File.WriteAllBytesAsync(file, [1, 2, 3]);
            await Assert.ThrowsAsync<CryptographicException>(() => store.LoadAsync("work"));
            await store.DeleteAsync("work");
            Assert.Null(await store.LoadAsync("work"));
        }
        finally
        {
            // This is the unique directory created by this test, never a computed user directory.
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
