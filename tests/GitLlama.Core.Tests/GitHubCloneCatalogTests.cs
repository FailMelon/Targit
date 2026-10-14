using System.Net;
using System.Text.Json;
using GitLlama.Providers;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitHubCloneCatalogTests
{
    [Fact]
    public async Task CatalogPaginatesAndRejectsUnsafeOrInaccessibleCloneUrls()
    {
        using var handler = new CatalogHandler();
        using var client = new HttpClient(handler);
        using var service = new GitHubCloneCatalog(client);
        var repos = await service.ListAsync("test-secret");
        Assert.Equal(101, repos.Count);
        Assert.Equal(2, handler.Requests);
        Assert.All(repos, repo => Assert.StartsWith("https://github.com/owner/", repo.CloneUrl));
        handler.Fail = true;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListAsync("test-secret"));
        Assert.DoesNotContain("test-secret", error.Message);
    }

    private sealed class CatalogHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public bool Fail { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            Assert.Equal("api.github.com", request.RequestUri!.Host);
            Assert.Equal("Bearer test-secret", request.Headers.Authorization!.ToString());
            object Item(string name, string url, bool pull = true) =>
                new
                {
                    name,
                    clone_url = url,
                    owner = new { login = "owner" },
                    permissions = new { pull },
                };
            var items = request.RequestUri.Query.EndsWith("&page=1", StringComparison.Ordinal)
                ? Enumerable
                    .Range(0, 100)
                    .Select(index => Item("repo" + index, "https://github.com/owner/repo" + index + ".git"))
                    .ToArray()
                : new[]
                {
                    Item("extra", "https://github.com/owner/extra.git"),
                    Item("unsafe", "https://attacker.example/repo"),
                    Item("embedded", "https://secret@github.com/owner/embedded.git"),
                    Item("denied", "https://github.com/owner/denied.git", false),
                };
            return Task.FromResult(
                new HttpResponseMessage(Fail ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
                {
                    Content = new StringContent(Fail ? "test-secret" : JsonSerializer.Serialize(items)),
                }
            );
        }
    }
}
