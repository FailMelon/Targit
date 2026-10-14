using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GitLlama.Core;

namespace GitLlama.Providers;

public enum GitHubPushAccess
{
    Allowed,
    Unknown,
    ApprovalNeeded,
    PrivateScopeNeeded,
    NoWritePermission,
    SignInRequired,
}

public sealed class GitHubPushAccessChecker : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public GitHubPushAccessChecker(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client =
            client
            ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
    }

    public async Task<GitHubPushAccess> CheckAsync(HostingRepository repository, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/repos/"
                + Uri.EscapeDataString(repository.Owner)
                + "/"
                + Uri.EscapeDataString(repository.Name)
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("GitLlama/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return GitHubPushAccess.SignInRequired;
        }

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0"))
            {
                return GitHubPushAccess.Unknown;
            }

            if (
                response.StatusCode == HttpStatusCode.NotFound
                && response.Headers.TryGetValues("X-OAuth-Scopes", out var scopes)
                && !scopes.SelectMany(value => value.Split(',')).Any(value => value.Trim() == "repo")
            )
            {
                return GitHubPushAccess.PrivateScopeNeeded;
            }

            return GitHubPushAccess.ApprovalNeeded;
        }

        if (!response.IsSuccessStatusCode)
        {
            return GitHubPushAccess.Unknown;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (
            json.RootElement.TryGetProperty("permissions", out var permissions)
            && permissions.TryGetProperty("push", out var push)
        )
        {
            return push.GetBoolean() ? GitHubPushAccess.Allowed : GitHubPushAccess.NoWritePermission;
        }

        return GitHubPushAccess.Unknown;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
