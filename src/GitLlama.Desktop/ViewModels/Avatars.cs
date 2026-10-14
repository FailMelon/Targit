using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GitLlama.Core;
using GitLlama.Providers;

namespace GitLlama.Desktop.ViewModels;

/// <summary>An author or remote picture. Initials are always available; the image arrives later, if at all.</summary>
public sealed partial class AvatarImage : ObservableObject
{
    private static readonly string[] Palette =
    [
        "#5B7FA6",
        "#7A5C99",
        "#4F8A72",
        "#A0684A",
        "#8A5068",
        "#5E8A99",
        "#8A7F48",
        "#5D6AA6",
    ];

    internal AvatarImage(string label, string? url, string? email = null, string? commitSha = null)
    {
        Url = url;
        Email = email;
        CommitSha = commitSha;
        var words = label.Split([' ', '.', '-', '_', '@'], StringSplitOptions.RemoveEmptyEntries);
        Initials = words.Length switch
        {
            0 => "?",
            1 => words[0][..1].ToUpperInvariant(),
            _ => (words[0][..1] + words[^1][..1]).ToUpperInvariant(),
        };
        Background = Palette[(int)(StableHash(label) % (uint)Palette.Length)];
    }

    internal string? Url { get; }
    internal string? Email { get; }

    /// <summary>One commit by this author, used to ask the hosting service which account made it.</summary>
    internal string? CommitSha { get; }
    public string Initials { get; }
    public string Background { get; }
    public bool HasImage => Image is not null;

    [ObservableProperty]
    private Bitmap? image;

    partial void OnImageChanged(Bitmap? value) => OnPropertyChanged(nameof(HasImage));

    private static uint StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value.ToLowerInvariant())
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }
}

/// <summary>Resolves author and remote pictures from public avatar services, cached in memory and on disk.</summary>
public static partial class AvatarCache
{
    private const int MaxImageBytes = 1024 * 1024;
    private static readonly TimeSpan Freshness = TimeSpan.FromDays(7);
    private static readonly ConcurrentDictionary<string, AvatarImage> Entries = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim Downloads = new(4);
    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitLlama", "1.0"));
        return client;
    });
    private static bool _enabled;
    private static IReadOnlyList<HostingRepository> _gitHubRepositories = [];

    /// <summary>False in tests and other offline hosts so bindings never reach the network.</summary>
    public static bool NetworkAvailable { get; set; } = true;
    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            foreach (var entry in Entries.Values)
            {
                if (value)
                {
                    _ = LoadAsync(entry);
                }
                else
                {
                    entry.Image = null;
                }
            }
        }
    }

    /// <summary>The open repository's GitHub remotes. Authors without a Gravatar are matched to their GitHub account through them.</summary>
    public static IReadOnlyList<HostingRepository> GitHubRepositories
    {
        get => _gitHubRepositories;
        set
        {
            if (_gitHubRepositories.SequenceEqual(value))
            {
                return;
            }

            _gitHubRepositories = value;
            if (!_enabled || value.Count == 0)
            {
                return;
            }

            foreach (var entry in Entries.Values.Where(e => e.CommitSha is not null && !e.HasImage))
            {
                _ = LoadAsync(entry);
            }
        }
    }

    /// <summary>Supplies the signed-in GitHub token, if any, for a higher API rate limit.</summary>
    public static Func<string>? GitHubToken { get; set; }

    public static AvatarImage ForAuthor(string name, string email, string? commitSha = null)
    {
        var label = string.IsNullOrWhiteSpace(name) ? email : name;
        return Get(
            "author:" + email.Trim().ToLowerInvariant() + "|" + label,
            () => new AvatarImage(label, AuthorAvatarUrl(email), email.Trim().ToLowerInvariant(), commitSha)
        );
    }

    public static AvatarImage ForRemote(string name, string url) =>
        Get("remote:" + url, () => new AvatarImage(name, RemoteAvatarUrl(url)));

    internal static string? AuthorAvatarUrl(string email)
    {
        email = email.Trim().ToLowerInvariant();
        if (email.Length == 0 || !email.Contains('@'))
        {
            return null;
        }
        // GitHub's private commit addresses identify the account directly.
        if (GitHubNoReply().Match(email) is { Success: true } noreply)
        {
            return noreply.Groups["id"].Success
                ? $"https://avatars.githubusercontent.com/u/{noreply.Groups["id"].Value}?s=64"
                : $"https://github.com/{Uri.EscapeDataString(noreply.Groups["login"].Value)}.png?size=64";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email))).ToLowerInvariant();
        return $"https://www.gravatar.com/avatar/{hash}?s=64&d=404";
    }

    internal static string? RemoteAvatarUrl(string url)
    {
        if (GitHubProvider.FromRemote(url) is { } repository)
        {
            return $"https://github.com/{Uri.EscapeDataString(repository.Owner)}.png?size=64";
        }

        var host = RemoteHost(url);
        if (host is null)
        {
            return null;
        }

        host = host switch
        {
            "ssh.dev.azure.com" or "vs-ssh.visualstudio.com" => "dev.azure.com",
            _ => host,
        };
        return $"https://{host}/favicon.ico";
    }

    private static string? RemoteHost(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return
                uri.Scheme is "https" or "http" or "ssh" or "git"
                && !uri.IsLoopback
                && uri.HostNameType == UriHostNameType.Dns
                ? uri.Host.ToLowerInvariant()
                : null;
        }
        // scp-like syntax: user@host:owner/repo.git
        var scp = ScpRemote().Match(url);
        return scp.Success ? scp.Groups["host"].Value.ToLowerInvariant() : null;
    }

    private static AvatarImage Get(string key, Func<AvatarImage> create) =>
        Entries.GetOrAdd(
            key,
            cacheKey =>
            {
                var avatar = create();
                if (_enabled)
                {
                    _ = LoadAsync(avatar);
                }

                return avatar;
            }
        );

    private static async Task LoadAsync(AvatarImage avatar)
    {
        if (!NetworkAvailable)
        {
            return;
        }

        try
        {
            var bytes = avatar.Url is { } url ? await ReadCachedAsync(url) ?? await DownloadAsync(url) : null;
            if (bytes is not { Length: > 0 } && await GitHubCommitAuthorAvatarAsync(avatar) is { } accountUrl)
            {
                bytes = await ReadCachedAsync(accountUrl) ?? await DownloadAsync(accountUrl);
            }

            if (bytes is not { Length: > 0 } || !_enabled)
            {
                return;
            }

            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            Dispatcher.UIThread.Post(() =>
            {
                if (_enabled)
                {
                    avatar.Image = bitmap;
                }
            });
        }
        catch
        { /* Unreachable hosts and undecodable images keep the initials. */
        }
    }

    private static string CachePath(string url) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GitLlama",
            "avatars",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant()
        );

    private static async Task<byte[]?> ReadCachedAsync(string url)
    {
        var file = new FileInfo(CachePath(url));
        // An empty file records a missing avatar so it is not requested again until it goes stale.
        return file.Exists && DateTime.UtcNow - file.LastWriteTimeUtc < Freshness
            ? await File.ReadAllBytesAsync(file.FullName)
            : null;
    }

    private static async Task<byte[]?> DownloadAsync(string url)
    {
        await Downloads.WaitAsync();
        try
        {
            using var response = await Http.Value.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            byte[] bytes = [];
            if (
                response.IsSuccessStatusCode
                && response.Content.Headers.ContentType?.MediaType?.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase
                ) != false
                && response.Content.Headers.ContentLength is null or <= MaxImageBytes
            )
            {
                await using var body = await response.Content.ReadAsStreamAsync();
                using var buffer = new MemoryStream();
                var chunk = new byte[16384];
                int read;
                while ((read = await body.ReadAsync(chunk)) > 0)
                {
                    if (buffer.Length + read > MaxImageBytes)
                    {
                        return null;
                    }

                    buffer.Write(chunk, 0, read);
                }

                bytes = buffer.ToArray();
            }
            // Only a definite "no picture" is remembered; rate limits and outages are retried later.
            else if (
                response.StatusCode is not (System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
                && !response.IsSuccessStatusCode
            )
            {
                return null;
            }

            await WriteCacheAsync(url, bytes);
            return bytes;
        }
        finally
        {
            Downloads.Release();
        }
    }

    private static async Task WriteCacheAsync(string key, byte[] bytes)
    {
        var path = CachePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
    }

    /// <summary>Asks GitHub which account made one of this author's commits and returns that account's picture.</summary>
    private static async Task<string?> GitHubCommitAuthorAvatarAsync(AvatarImage avatar)
    {
        if (avatar.CommitSha is not { } sha || avatar.Email is not { } email)
        {
            return null;
        }
        // The commit may exist on only some remotes, such as a fork that is behind.
        foreach (var repository in _gitHubRepositories)
        {
            if (await GitHubCommitAuthorAvatarAsync(repository, sha, email) is { } url)
            {
                return url;
            }
        }

        return null;
    }

    private static async Task<string?> GitHubCommitAuthorAvatarAsync(
        HostingRepository repository,
        string sha,
        string email
    )
    {
        var key = $"github-author:{repository.Owner}/{repository.Name}:{email}";
        if (await ReadCachedAsync(key) is { } cached)
        {
            return cached.Length > 0 ? Encoding.UTF8.GetString(cached) : null;
        }

        await Downloads.WaitAsync();
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}/commits/{Uri.EscapeDataString(sha)}"
            );
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            if (GitHubToken?.Invoke() is { Length: > 0 } token)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await Http.Value.SendAsync(request);
            // Unpushed commits, private repositories and rate limits are retried next session.
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var accountUrl = GitHubAvatarFromCommit(json.RootElement);
            await WriteCacheAsync(key, Encoding.UTF8.GetBytes(accountUrl ?? ""));
            return accountUrl;
        }
        finally
        {
            Downloads.Release();
        }
    }

    internal static string? GitHubAvatarFromCommit(JsonElement commit)
    {
        if (
            !commit.TryGetProperty("author", out var author)
            || author.ValueKind != JsonValueKind.Object
            || !author.TryGetProperty("avatar_url", out var avatarUrl)
            || avatarUrl.GetString() is not { } url
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || !uri.Host.EndsWith("githubusercontent.com", StringComparison.OrdinalIgnoreCase)
        )
        {
            return null;
        }

        return url + (url.Contains('?') ? "&" : "?") + "s=64";
    }

    [GeneratedRegex(@"^(?:(?<id>\d+)\+)?(?<login>[a-z0-9](?:[a-z0-9-]{0,38}))@users\.noreply\.github\.com$")]
    private static partial Regex GitHubNoReply();

    [GeneratedRegex(@"^(?:[^@/\s]+@)?(?<host>[A-Za-z0-9.-]+\.[A-Za-z]{2,}):(?!//)")]
    private static partial Regex ScpRemote();
}
