using System.Net;
using GitLlama.Core;
using GitLlama.Providers;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitHubPushAccessTests
{
    [Theory]
    [InlineData(200, "{\"permissions\":{\"push\":true}}", null, false, GitHubPushAccess.Allowed)]
    [InlineData(200, "{\"permissions\":{\"push\":false}}", null, false, GitHubPushAccess.NoWritePermission)]
    [InlineData(404, "token must never appear", "public_repo, read:user", false, GitHubPushAccess.PrivateScopeNeeded)]
    [InlineData(404, "", "repo, read:user", false, GitHubPushAccess.ApprovalNeeded)]
    [InlineData(403, "", null, false, GitHubPushAccess.ApprovalNeeded)]
    [InlineData(403, "", null, true, GitHubPushAccess.Unknown)]
    [InlineData(401, "", null, false, GitHubPushAccess.SignInRequired)]
    [InlineData(500, "", null, false, GitHubPushAccess.Unknown)]
    public async Task ChecksDistinguishAccessFailuresFromWritePermissionsAndOutages(
        int status,
        string body,
        string? scopes,
        bool rateLimited,
        GitHubPushAccess expected
    )
    {
        using var handler = new Handler(status, body, scopes, rateLimited);
        using var client = new HttpClient(handler);
        using var service = new GitHubPushAccessChecker(client);
        Assert.Equal(
            expected,
            await service.CheckAsync(new HostingRepository("owner", "repo"), "secret-token", CancellationToken.None)
        );
    }

    private sealed class Handler(int status, string body, string? scopes, bool rateLimited) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("https://api.github.com/repos/owner/repo", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer secret-token", request.Headers.Authorization!.ToString());
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
            if (scopes is not null)
            {
                response.Headers.TryAddWithoutValidation("X-OAuth-Scopes", scopes);
            }

            if (rateLimited)
            {
                response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
            }

            return Task.FromResult(response);
        }
    }
}
