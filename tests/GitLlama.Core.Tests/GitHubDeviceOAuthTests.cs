using System.Net;
using GitLlama.Providers;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class GitHubDeviceOAuthTests
{
    private const string Device = "{\"device_code\":\"device-secret\",\"user_code\":\"ABCD-EFGH\",\"expires_in\":900,\"interval\":5,\"verification_uri\":\"https://github.com/login/device\"}";
    [Fact]
    public async Task PollingRespectsPendingAndSlowDownAndReadsTokenExpiry()
    {
        using var handler = new Handler(Device, "{\"error\":\"authorization_pending\"}", "{\"error\":\"slow_down\",\"interval\":10}", "{\"access_token\":\"oauth-secret\",\"token_type\":\"bearer\",\"expires_in\":28800,\"refresh_token\":\"refresh-secret\"}");
        using var client = new HttpClient(handler); var now = DateTimeOffset.UtcNow; var waits = new List<int>();
        using var oauth = new GitHubDeviceOAuth(client, () => now, (delay, ct) => { ct.ThrowIfCancellationRequested(); waits.Add((int)delay.TotalSeconds); now += delay; return Task.CompletedTask; });
        var device = await oauth.BeginAsync("client-id", false);
        Assert.Equal("ABCD-EFGH", device.UserCode); Assert.Equal("https://github.com/login/device", device.VerificationUri.AbsoluteUri);
        var token = await oauth.CompleteAsync("client-id", device);
        Assert.Equal(new[] { 5, 5, 10 }, waits); Assert.Equal("oauth-secret", token.AccessToken); Assert.Equal(now.AddHours(8), token.ExpiresAt);
        Assert.Equal("refresh-secret", token.RefreshToken);
        Assert.DoesNotContain("oauth-secret", token.ToString()); Assert.DoesNotContain("device-secret", device.ToString());
        Assert.Contains("scope=public_repo+read%3Auser+workflow", handler.Bodies[0]);
        Assert.All(handler.Urls, url => Assert.StartsWith("https://github.com/login/", url));
        Assert.All(handler.Bodies, body => Assert.DoesNotContain("client_secret", body));
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", handler.Bodies[1]);
    }
    [Theory]
    [InlineData("access_denied", "declined")]
    [InlineData("expired_token", "expired")]
    [InlineData("device_flow_disabled", "Enable Device Flow")]
    [InlineData("incorrect_client_credentials", "Client ID")]
    [InlineData("unexpected", "failed")]
    public async Task TerminalErrorsStopPollingAndNeverEchoResponseSecrets(string code, string expected)
    {
        using var handler = new Handler(Device, "{\"error\":\"" + code + "\",\"error_description\":\"device-secret oauth-secret\"}");
        using var client = new HttpClient(handler); using var oauth = new GitHubDeviceOAuth(client, delay: (_, _) => Task.CompletedTask);
        var device = await oauth.BeginAsync("client", true); Assert.Contains("scope=repo+read%3Auser+workflow", handler.Bodies[0]);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => oauth.CompleteAsync("client", device));
        Assert.Contains(expected, error.Message); Assert.DoesNotContain("secret", error.Message); Assert.Equal(2, handler.Bodies.Count);
    }
    [Fact]
    public async Task ExpiredCodesAndCancellationStopWithoutTokenRequests()
    {
        using var handler = new Handler(Device); using var client = new HttpClient(handler); var now = DateTimeOffset.UtcNow;
        using var oauth = new GitHubDeviceOAuth(client, () => now, (_, _) => Task.CompletedTask);
        var device = await oauth.BeginAsync("client", false); now += TimeSpan.FromMinutes(16);
        await Assert.ThrowsAsync<InvalidOperationException>(() => oauth.CompleteAsync("client", device));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => oauth.CompleteAsync("client", device, cts.Token));
        Assert.Single(handler.Bodies);
    }
    [Fact]
    public async Task AuthorizationAlwaysUsesTheFixedGitHubVerificationPage()
    {
        using var handler = new Handler(Device.Replace("https://github.com/login/device", "https://attacker.example"));
        using var client = new HttpClient(handler); using var oauth = new GitHubDeviceOAuth(client);
        Assert.Equal("https://github.com/login/device", (await oauth.BeginAsync("client", false)).VerificationUri.AbsoluteUri);
    }
    [Fact]
    public async Task RenewalRotatesBothTokensWithoutAClientSecret()
    {
        using var handler = new Handler("{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"token_type\":\"bearer\",\"expires_in\":28800,\"refresh_token_expires_in\":15000000}");
        using var client = new HttpClient(handler); var now = DateTimeOffset.UtcNow;
        using var oauth = new GitHubDeviceOAuth(client, () => now);
        var token = await oauth.RefreshAsync("client", "old-refresh");
        Assert.Equal("new-access", token.AccessToken); Assert.Equal("new-refresh", token.RefreshToken);
        Assert.Equal(now.AddHours(8), token.ExpiresAt); Assert.Equal(now.AddSeconds(15000000), token.RefreshExpiresAt);
        Assert.Contains("grant_type=refresh_token", handler.Bodies.Single()); Assert.Contains("refresh_token=old-refresh", handler.Bodies.Single()); Assert.DoesNotContain("client_secret", handler.Bodies.Single());
    }
    [Fact]
    public async Task RejectedRefreshRequiresSignInWithoutEchoingSecrets()
    {
        using var handler = new Handler("{\"error\":\"bad_refresh_token\",\"error_description\":\"old-refresh secret\"}");
        using var client = new HttpClient(handler); using var oauth = new GitHubDeviceOAuth(client);
        var error = await Assert.ThrowsAsync<GitHubRefreshRejectedException>(() => oauth.RefreshAsync("client", "old-refresh"));
        Assert.Contains("revoked", error.Message); Assert.DoesNotContain("secret", error.Message);
    }
    private sealed class Handler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);
        public List<string> Bodies { get; } = [];
        public List<string> Urls { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct)); Urls.Add(request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_responses.Dequeue()) };
        }
    }
}
