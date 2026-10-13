using System.Net.Http.Headers;
using System.Text.Json;

namespace GitLlama.Providers;

public sealed record CloneableRepository(string Provider, string Group, string Name, string CloneUrl)
{
    public string Source => Provider + " · " + Group;
}

public sealed class GitHubCloneCatalog : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    public GitHubCloneCatalog(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    }
    public async Task<IReadOnlyList<CloneableRepository>> ListAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Connect your GitHub account first.");
        var repositories = new List<CloneableRepository>();
        for (var page = 1; ; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/repos?per_page=100&sort=full_name&direction=asc&page=" + page);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.UserAgent.ParseAdd("GitLlama/1.0"); request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("GitHub repositories could not be loaded. Check your connection and account permissions.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct); using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            foreach (var item in json.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("disabled", out var disabled) && disabled.GetBoolean()) continue;
                if (item.TryGetProperty("permissions", out var permissions) && permissions.TryGetProperty("pull", out var pull) && !pull.GetBoolean()) continue;
                var url = item.GetProperty("clone_url").GetString() ?? "";
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) continue;
                repositories.Add(new("GitHub", item.GetProperty("owner").GetProperty("login").GetString() ?? "", item.GetProperty("name").GetString() ?? "", uri.AbsoluteUri));
            }
            if (json.RootElement.GetArrayLength() < 100) break;
        }
        return repositories.DistinctBy(repo => repo.CloneUrl).ToArray();
    }
    public void Dispose() { if (_ownsClient) _client.Dispose(); }
}
