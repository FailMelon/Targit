using System.Net.Http.Headers;
using System.Text.Json;

namespace GitLlama.Providers;

public sealed class GitHubDeviceAuthorization
{
    internal string DeviceCode { get; init; } = "";
    public required string UserCode { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    internal int IntervalSeconds { get; init; }
    public Uri VerificationUri => new("https://github.com/login/device");
}

public sealed class GitHubOAuthToken
{
    public required string AccessToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string RefreshToken { get; init; } = "";
    public DateTimeOffset? RefreshExpiresAt { get; init; }
}

public sealed class GitHubRefreshRejectedException : InvalidOperationException
{
    public GitHubRefreshRejectedException()
        : base("GitHub authorization expired or was revoked. Sign in again.") { }
}

/// <summary>GitHub device OAuth: no client secret, fixed HTTPS endpoints and session-only tokens.</summary>
public sealed class GitHubDeviceOAuth : IDisposable
{
    public const string DefaultClientId = "Ov23liO3PME31PJH8PfM";
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public GitHubDeviceOAuth(
        HttpClient? client = null,
        Func<DateTimeOffset>? now = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null
    )
    {
        _ownsClient = client is null;
        _client =
            client
            ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _delay = delay ?? ((duration, ct) => Task.Delay(duration, ct));
    }

    public async Task<GitHubDeviceAuthorization> BeginAsync(
        string clientId,
        bool includePrivateRepositories,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        using var json = await PostAsync(
            "login/device/code",
            new()
            {
                ["client_id"] = clientId,
                ["scope"] = includePrivateRepositories ? "repo read:user workflow" : "public_repo read:user workflow",
            },
            ct
        );
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("GitHub returned an invalid sign-in response. Try again.");
        }

        if (root.TryGetProperty("error", out var error))
        {
            throw Failure(error.GetString());
        }

        var device = Text(root, "device_code");
        var user = Text(root, "user_code");
        if (
            device.Length == 0
            || user.Length == 0
            || !root.TryGetProperty("expires_in", out var expires)
            || !expires.TryGetInt32(out var seconds)
            || seconds is < 1 or > 3600
        )
        {
            throw new InvalidOperationException("GitHub returned an invalid sign-in response. Try again.");
        }

        var interval =
            root.TryGetProperty("interval", out var value) && value.TryGetInt32(out var number)
                ? Math.Clamp(number, 1, 3600)
                : 5;
        return new GitHubDeviceAuthorization
        {
            DeviceCode = device,
            UserCode = user,
            ExpiresAt = _now().AddSeconds(seconds),
            IntervalSeconds = interval,
        };
    }

    public async Task<GitHubOAuthToken> CompleteAsync(
        string clientId,
        GitHubDeviceAuthorization authorization,
        CancellationToken ct = default
    )
    {
        var interval = authorization.IntervalSeconds;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (_now().AddSeconds(interval) >= authorization.ExpiresAt)
            {
                throw Failure("expired_token");
            }

            await _delay(TimeSpan.FromSeconds(interval), ct);
            ct.ThrowIfCancellationRequested();
            if (_now() >= authorization.ExpiresAt)
            {
                throw Failure("expired_token");
            }

            using var json = await PostAsync(
                "login/oauth/access_token",
                new()
                {
                    ["client_id"] = clientId,
                    ["device_code"] = authorization.DeviceCode,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                },
                ct
            );
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("GitHub returned an invalid sign-in response. Try again.");
            }

            if (root.TryGetProperty("error", out var error))
            {
                var code = error.GetString();
                if (code == "authorization_pending")
                {
                    continue;
                }

                if (code == "slow_down")
                {
                    interval += 5;
                    if (root.TryGetProperty("interval", out var next) && next.TryGetInt32(out var number))
                    {
                        interval = Math.Max(interval, number);
                    }

                    continue;
                }

                throw Failure(code);
            }

            return ParseToken(root);
        }
    }

    public async Task<GitHubOAuthToken> RefreshAsync(
        string clientId,
        string refreshToken,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        using var json = await PostAsync(
            "login/oauth/access_token",
            new()
            {
                ["client_id"] = clientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            },
            ct
        );
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("GitHub returned an invalid renewal response.");
        }

        if (root.TryGetProperty("error", out var error))
        {
            if (
                error.GetString()
                is "bad_refresh_token"
                    or "invalid_grant"
                    or "access_denied"
                    or "incorrect_client_credentials"
            )
            {
                throw new GitHubRefreshRejectedException();
            }

            throw new InvalidOperationException("GitHub renewal is temporarily unavailable.");
        }

        var result = ParseToken(root);
        if (result.RefreshToken.Length == 0)
        {
            throw new InvalidOperationException("GitHub returned an invalid renewal response.");
        }

        return result;
    }

    private GitHubOAuthToken ParseToken(JsonElement root)
    {
        var token = Text(root, "access_token");
        if (token.Length == 0 || Text(root, "token_type") != "bearer")
        {
            throw new InvalidOperationException("GitHub did not return a valid access token. Try signing in again.");
        }

        DateTimeOffset? Expiry(string name)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (!value.TryGetInt32(out var seconds) || seconds <= 0)
            {
                throw new InvalidOperationException("GitHub returned an invalid token expiry.");
            }

            return _now().AddSeconds(seconds);
        }

        return new GitHubOAuthToken
        {
            AccessToken = token,
            ExpiresAt = Expiry("expires_in"),
            RefreshToken = Text(root, "refresh_token"),
            RefreshExpiresAt = Expiry("refresh_token_expires_in"),
        };
    }

    private async Task<JsonDocument> PostAsync(string path, Dictionary<string, string> values, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/" + path)
        {
            Content = new FormUrlEncodedContent(values),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("GitLlama/1.0");
        using var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("GitHub sign-in is unavailable. Check your connection and try again.");
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("GitHub returned an invalid sign-in response. Try again.");
        }
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static InvalidOperationException Failure(string? code) =>
        new(
            code switch
            {
                "access_denied" => "GitHub sign-in was declined. You can try again.",
                "expired_token" or "token_expired" => "The GitHub sign-in code expired. Start sign-in again.",
                "device_flow_disabled" => "Enable Device Flow in the GitLlama OAuth app's GitHub settings.",
                "incorrect_client_credentials" => "The GitHub OAuth Client ID is invalid. Check the app registration.",
                _ => "GitHub sign-in failed. Start sign-in again.",
            }
        );

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
