using System.Net.Http.Headers;
using System.Text.Json;
using GitLlama.Core;

namespace GitLlama.Providers;

public sealed record AzureRepository(string Id, string Name, string Project, string CloneUrl, string WebUrl);
public sealed class AzureDevOpsService : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    public AzureDevOpsService(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    }
    private static AuthenticationHeaderValue Authorization(string token) => new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(":" + token)));
    public async Task<IReadOnlyList<AzureRepository>> RepositoriesAsync(string organization, string token, CancellationToken ct = default)
    {
        organization = AzureDevOpsRemote.NormalizeOrganization(organization);
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsControl)) throw new InvalidOperationException("Enter a valid Azure DevOps personal access token.");
        var repositories = new List<AzureRepository>(); string? continuation = null;
        do
        {
            var endpoint = "https://dev.azure.com/" + Uri.EscapeDataString(organization) + "/_apis/git/repositories?api-version=7.1" + (continuation is null ? "" : "&continuationToken=" + Uri.EscapeDataString(continuation));
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = Authorization(token); request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException((int)response.StatusCode is >= 300 and < 400 or 401 or 403 ? "Azure DevOps denied access. Check the token expiry, organization and Code (Read) permission." : "Azure DevOps is unavailable. Check your connection and try again.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct); using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            foreach (var item in json.RootElement.GetProperty("value").EnumerateArray())
            {
                var raw = item.GetProperty("remoteUrl").GetString() ?? ""; var target = AzureDevOpsRemote.Parse(raw);
                if (target is null || !target.Organization.Equals(organization, StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(raw, UriKind.Absolute, out var clone) || clone.Scheme != "https") continue;
                var clean = new UriBuilder(clone) { UserName = "", Password = "" }.Uri.AbsoluteUri;
                repositories.Add(new(item.GetProperty("id").GetString() ?? "", item.GetProperty("name").GetString() ?? target.Repository, item.GetProperty("project").GetProperty("name").GetString() ?? target.Project, clean, "https://dev.azure.com/" + Uri.EscapeDataString(organization) + "/" + Uri.EscapeDataString(target.Project) + "/_git/" + Uri.EscapeDataString(target.Repository)));
            }
            continuation = response.Headers.TryGetValues("x-ms-continuationtoken", out var values) ? values.FirstOrDefault() : null;
        } while (!string.IsNullOrEmpty(continuation));
        return repositories.OrderBy(repo => repo.Project).ThenBy(repo => repo.Name).ToArray();
    }
    public void Dispose() { if (_ownsClient) _client.Dispose(); }
}
