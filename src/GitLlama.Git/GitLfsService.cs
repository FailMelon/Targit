using System.Text.Json;
using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitLfsFile(string Path, bool IsDownloaded);

/// <summary>
/// What <c>git lfs prune</c> would delete: <see cref="Objects"/> local copies of old LFS content the server
/// already has, out of local LFS storage using <see cref="StorageBytes"/>.
/// </summary>
public sealed record GitLfsPrunePreview(int Objects, long StorageBytes);

/// <summary>A <c>filter=lfs</c> line from a .gitattributes file in <see cref="Folder"/> ("" for the root).</summary>
public sealed partial record GitLfsRule(string Folder, string Pattern)
{
    private Regex? _regex;

    /// <summary>Whether <paramref name="path"/> (repository-relative, with forward slashes) matches this rule.</summary>
    public bool Matches(string path)
    {
        path = path.Replace('\\', '/');
        if (Folder.Length > 0 && !path.StartsWith(Folder, StringComparison.Ordinal))
        {
            return false;
        }

        var relative = path[Folder.Length..];
        var pattern = Pattern.TrimStart('/');
        // As in .gitattributes, a pattern without a slash matches the file name at any depth.
        var subject = Pattern.Contains('/') ? relative : relative[(relative.LastIndexOf('/') + 1)..];
        _regex ??= new Regex("^" + GlobToRegex(pattern) + "$", RegexOptions.CultureInvariant);
        return _regex.IsMatch(subject);
    }

    private static string GlobToRegex(string glob)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                var slash = i + 2 < glob.Length && glob[i + 2] == '/';
                builder.Append(slash ? "(?:.*/)?" : ".*");
                i += slash ? 2 : 1;
            }
            else if (c == '*')
            {
                builder.Append("[^/]*");
            }
            else if (c == '?')
            {
                builder.Append("[^/]");
            }
            else if (c == '[' && glob.IndexOf(']', i + 1) is var end and > 0)
            {
                builder.Append('[').Append(glob[(i + 1)..end].Replace("\\", "\\\\")).Append(']');
                i = end;
            }
            else
            {
                builder.Append(Regex.Escape(c.ToString()));
            }
        }

        return builder.ToString();
    }
}

/// <summary>A file locked on the LFS server. <see cref="IsOurs"/> is true when the current user holds it.</summary>
public sealed record GitLfsLock(string Id, string Path, string Owner, DateTimeOffset LockedAt, bool IsOurs);

public sealed record GitLfsStatus(
    string? Version,
    bool IsSetUp,
    IReadOnlyList<string> Patterns,
    IReadOnlyList<GitLfsFile> Files
)
{
    public bool IsInstalled => Version is not null;
    public bool UsesLfs => Patterns.Count > 0 || Files.Count > 0;
    public int MissingCount => Files.Count(file => !file.IsDownloaded);

    /// <summary>Patterns marked lockable: matching files are read-only until you lock them.</summary>
    public IReadOnlyList<string> LockablePatterns { get; init; } = [];

    public IReadOnlyList<GitLfsRule> Rules { get; init; } = [];

    /// <summary>Whether <paramref name="path"/> is stored in Git LFS by the current .gitattributes.</summary>
    public bool IsLfsPath(string path) => Rules.Any(rule => rule.Matches(path));

    public static GitLfsStatus None { get; } = new(null, false, [], []);
}

/// <summary>Git LFS operations through the git-lfs command-line tool.</summary>
public sealed partial class GitLfsService
{
    private static readonly GitNetworkOptions Local = new();

    public async Task<GitLfsStatus> GetStatusAsync(string path, CancellationToken ct = default)
    {
        var attributes = await Task.Run(() => TrackedAttributes(path), ct);
        var patterns = attributes.Select(entry => entry.Pattern).ToArray();
        var lockable = attributes.Where(entry => entry.Lockable).Select(entry => entry.Pattern).ToArray();
        var rules = attributes.Select(entry => entry.Rule).ToArray();
        var version = await VersionAsync(ct);
        if (version is null)
        {
            return new GitLfsStatus(null, false, patterns, []) { LockablePatterns = lockable, Rules = rules };
        }

        var setUp = await TryRunAsync(path, ["config", "--get", "filter.lfs.process"], ct) is { Length: > 0 };
        IReadOnlyList<GitLfsFile> files = [];
        if (patterns.Length > 0 && await TryRunAsync(path, ["lfs", "ls-files"], ct) is { } listing)
        {
            files = ParseFiles(listing);
        }

        return new GitLfsStatus(version, setUp, patterns, files) { LockablePatterns = lockable, Rules = rules };
    }

    private static string? _version;

    /// <summary>The installed git-lfs version, or null when it isn't installed. Found once per session.</summary>
    public static async Task<string?> VersionAsync(CancellationToken ct = default) =>
        _version ??= (await TryRunAsync(null, ["lfs", "version"], ct))?.Trim() is { Length: > 0 } version
            ? version.Split(' ')[0].Replace("git-lfs/", "")
            : null;

    /// <summary>Whether any .gitattributes in the repository routes files through LFS.</summary>
    public static bool UsesLfs(Repository repository) => TrackedPatterns(repository).Count > 0;

    /// <summary>Starts tracking files that match <paramref name="pattern"/>, such as "*.psd", in .gitattributes.</summary>
    public Task TrackAsync(string path, string pattern, CancellationToken ct = default) =>
        GitSystemTransport.RunAsync(path, ["lfs", "track", "--", ValidPattern(pattern)], Local, ct);

    /// <summary>Stops tracking <paramref name="pattern"/>. Files already committed to LFS stay in LFS history.</summary>
    public Task UntrackAsync(string path, string pattern, CancellationToken ct = default) =>
        GitSystemTransport.RunAsync(path, ["lfs", "untrack", "--", ValidPattern(pattern)], Local, ct);

    /// <summary>Configures this repository's LFS filters and push hook for command-line Git.</summary>
    public Task SetUpAsync(string path, CancellationToken ct = default) =>
        GitSystemTransport.RunAsync(path, ["lfs", "install", "--local"], Local, ct);

    /// <summary>Downloads LFS content for the checked-out files that are still pointers.</summary>
    public Task DownloadAsync(
        string path,
        string? remoteUrl,
        GitNetworkOptions network,
        CancellationToken ct = default
    ) => Run(path, ["lfs", "pull"], remoteUrl, network, ct);

    /// <summary>Uploads the LFS objects referenced by <paramref name="reference"/> before its commits are pushed.</summary>
    public Task PushAsync(
        string path,
        string remote,
        string remoteUrl,
        string reference,
        GitNetworkOptions network,
        CancellationToken ct = default
    ) => Run(path, ["lfs", "push", "--", remote, reference], remoteUrl, network, ct);

    /// <summary>Marks files matching <paramref name="pattern"/> lockable (read-only until locked), or not.</summary>
    public Task SetLockableAsync(string path, string pattern, bool lockable, CancellationToken ct = default) =>
        GitSystemTransport.RunAsync(
            path,
            ["lfs", "track", lockable ? "--lockable" : "--not-lockable", "--", ValidPattern(pattern)],
            Local,
            ct
        );

    /// <summary>Uploads LFS content for every branch and tag, repairing commits whose content never reached the server.</summary>
    public Task PushAllAsync(
        string path,
        string remote,
        string remoteUrl,
        GitNetworkOptions network,
        CancellationToken ct = default
    ) => Run(path, ["lfs", "push", "--all", "--", remote], remoteUrl, network, ct);

    /// <summary>Counts what <see cref="PruneAsync"/> would delete, without deleting anything.</summary>
    public async Task<GitLfsPrunePreview> PreviewPruneAsync(string path, CancellationToken ct = default)
    {
        // git-lfs's verbose list can omit entries, but its summary line is exact:
        // "prune: 3 local objects, 1 retained, done."
        var output = await GitSystemTransport.RunAsync(path, ["lfs", "prune", "--dry-run"], Local, ct);
        var summary = PruneSummary().Match(output);
        var objects = summary.Success
            ? int.Parse(summary.Groups["local"].Value) - int.Parse(summary.Groups["retained"].Value)
            : 0;
        return new GitLfsPrunePreview(Math.Max(0, objects), StorageSize(path));
    }

    /// <summary>
    /// Deletes local copies of LFS content that isn't needed for recent commits and is already on the server.
    /// Content that hasn't been pushed is always kept.
    /// </summary>
    /// <returns>The number of bytes freed.</returns>
    public async Task<long> PruneAsync(string path, CancellationToken ct = default)
    {
        var before = StorageSize(path);
        await GitSystemTransport.RunAsync(path, ["lfs", "prune"], Local, ct);
        return Math.Max(0, before - StorageSize(path));
    }

    /// <summary>The size of this repository's local LFS content.</summary>
    public static long StorageSize(string path)
    {
        using var repository = new Repository(path);
        var storage = new DirectoryInfo(Path.Combine(repository.Info.Path, "lfs", "objects"));
        return storage.Exists ? storage.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length) : 0;
    }

    /// <summary>Lists locks on the LFS server, marking the ones the current user holds.</summary>
    public async Task<IReadOnlyList<GitLfsLock>> GetLocksAsync(
        string path,
        string? remoteUrl,
        GitNetworkOptions network,
        CancellationToken ct = default
    ) => ParseLocks(await Run(path, ["lfs", "locks", "--verify", "--json"], remoteUrl, network, ct));

    /// <summary>Locks <paramref name="file"/> so only you can push changes to it, and makes it writable.</summary>
    public Task LockAsync(
        string path,
        string file,
        string? remoteUrl,
        GitNetworkOptions network,
        CancellationToken ct = default
    ) => Run(path, ["lfs", "lock", "--", RepositoryPath(file)], remoteUrl, network, ct);

    /// <summary>
    /// Releases the lock on <paramref name="file"/>. <paramref name="force"/> releases someone else's lock or one
    /// on a file with uncommitted changes.
    /// </summary>
    public Task UnlockAsync(
        string path,
        string file,
        bool force,
        string? remoteUrl,
        GitNetworkOptions network,
        CancellationToken ct = default
    )
    {
        List<string> arguments = ["lfs", "unlock"];
        if (force)
        {
            arguments.Add("--force");
        }

        arguments.AddRange(["--", RepositoryPath(file)]);
        return Run(path, arguments, remoteUrl, network, ct);
    }

    /// <summary>
    /// Makes lockable files read-only unless you hold their lock, as Git's post-checkout hook does. LibGit2Sharp
    /// checkouts don't run hooks, so this runs after the checked-out commit changes.
    /// </summary>
    public Task ApplyLockablePermissionsAsync(string path, CancellationToken ct = default) =>
        GitSystemTransport.RunAsync(path, ["lfs", "post-checkout", new string('0', 40), "HEAD", "1"], Local, ct);

    /// <summary>Locks held by someone else on files changed by the commits that would be pushed.</summary>
    public static IReadOnlyList<GitLfsLock> LockConflicts(
        Repository repository,
        string remote,
        IEnumerable<GitLfsLock> locks
    )
    {
        var theirs = locks.Where(entry => !entry.IsOurs).ToArray();
        if (theirs.Length == 0 || repository.Head.Tip is null)
        {
            return [];
        }

        var pushed = repository
            .Refs.Where(reference =>
                reference.CanonicalName.StartsWith($"refs/remotes/{remote}/", StringComparison.Ordinal)
            )
            .ToArray();
        var commits = repository.Commits.QueryBy(
            new CommitFilter { IncludeReachableFrom = repository.Head.Tip, ExcludeReachableFrom = pushed }
        );
        var changed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var commit in commits)
        {
            var parent = commit.Parents.FirstOrDefault()?.Tree;
            foreach (var change in repository.Diff.Compare<TreeChanges>(parent, commit.Tree))
            {
                changed.Add(change.Path);
                changed.Add(change.OldPath);
            }
        }

        return theirs.Where(entry => changed.Contains(entry.Path)).OrderBy(entry => entry.Path).ToArray();
    }

    public static IReadOnlyList<GitLfsLock> ParseLocks(string json)
    {
        using var document = JsonDocument.Parse(json);
        var locks = new List<GitLfsLock>();
        void Read(string property, bool ours)
        {
            if (
                document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(property, out var list)
                || list.ValueKind != JsonValueKind.Array
            )
            {
                return;
            }

            foreach (var item in list.EnumerateArray())
            {
                locks.Add(
                    new GitLfsLock(
                        item.TryGetProperty("id", out var id) ? id.ToString() : "",
                        item.GetProperty("path").GetString() ?? "",
                        item.TryGetProperty("owner", out var owner) && owner.TryGetProperty("name", out var name)
                            ? name.GetString() ?? ""
                            : "",
                        item.TryGetProperty("locked_at", out var at) && at.TryGetDateTimeOffset(out var when)
                            ? when
                            : DateTimeOffset.MinValue,
                        ours
                    )
                );
            }
        }

        Read("ours", true);
        Read("theirs", false);
        return locks.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<GitLfsFile> ParseFiles(string listing) =>
        listing
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // "<oid> * <path>" when downloaded, "<oid> - <path>" when only the pointer is present.
            .Select(line => line.Split(' ', 3))
            .Where(parts => parts.Length == 3 && parts[1] is "*" or "-")
            .Select(parts => new GitLfsFile(parts[2], parts[1] == "*"))
            .ToArray();

    private static IReadOnlyList<(string Pattern, bool Lockable, GitLfsRule Rule)> TrackedAttributes(string path)
    {
        using var repository = new Repository(path);
        return TrackedAttributes(repository);
    }

    private static IReadOnlyList<string> TrackedPatterns(Repository repository) =>
        TrackedAttributes(repository).Select(entry => entry.Pattern).ToArray();

    private static IReadOnlyList<(string Pattern, bool Lockable, GitLfsRule Rule)> TrackedAttributes(
        Repository repository
    )
    {
        if (repository.Info.WorkingDirectory is not { } root)
        {
            return [];
        }

        var files = repository
            .Index.Select(entry => entry.Path)
            .Where(entry => entry == ".gitattributes" || entry.EndsWith("/.gitattributes", StringComparison.Ordinal))
            .Prepend(".gitattributes")
            .Distinct();
        var patterns = new List<(string Pattern, bool Lockable, GitLfsRule Rule)>();
        foreach (var file in files)
        {
            var absolute = Path.Combine(root, file);
            if (!File.Exists(absolute))
            {
                continue;
            }

            var folder = Path.GetDirectoryName(file)?.Replace('\\', '/') is { Length: > 0 } directory
                ? directory + "/"
                : "";
            foreach (var line in File.ReadLines(absolute))
            {
                var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && !parts[0].StartsWith('#') && parts.Skip(1).Contains("filter=lfs"))
                {
                    patterns.Add(
                        (folder + parts[0], parts.Skip(1).Contains("lockable"), new GitLfsRule(folder, parts[0]))
                    );
                }
            }
        }

        return patterns.DistinctBy(entry => entry.Pattern).ToArray();
    }

    [GeneratedRegex(@"prune: (?<local>\d+) local objects?, (?<retained>\d+) retained")]
    private static partial Regex PruneSummary();

    private static string RepositoryPath(string file)
    {
        file = file.Trim().Replace('\\', '/');
        if (file.Length == 0 || file.Any(char.IsControl) || file.StartsWith('-') || Path.IsPathRooted(file))
        {
            throw new ArgumentException("Choose a file inside the repository.", nameof(file));
        }

        return file;
    }

    private static string ValidPattern(string pattern)
    {
        pattern = pattern.Trim();
        if (pattern.Length == 0 || pattern.Any(char.IsControl) || pattern.StartsWith('-'))
        {
            throw new ArgumentException("Enter a file pattern such as *.psd.", nameof(pattern));
        }

        return pattern;
    }

    private static Task<string> Run(
        string path,
        IEnumerable<string> arguments,
        string? remoteUrl,
        GitNetworkOptions network,
        CancellationToken ct
    )
    {
        var ssh = remoteUrl is not null && GitSystemTransport.IsSsh(remoteUrl);
        // A session token is only sent to the host it belongs to.
        var tokenHost =
            remoteUrl is not null
            && !string.IsNullOrEmpty(network.Secret)
            && Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == "https"
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.Equals(uri.Authority, network.CredentialHost, StringComparison.OrdinalIgnoreCase);
        if (remoteUrl is not null)
        {
            network.EnsureAzureAccess(remoteUrl);
        }

        return GitSystemTransport.RunAsync(
            path,
            arguments,
            network,
            ct,
            ssh: ssh,
            sshRemoteUrl: ssh ? remoteUrl : null,
            httpsRemoteUrl: tokenHost ? remoteUrl : null
        );
    }

    private static async Task<string?> TryRunAsync(string? path, IEnumerable<string> arguments, CancellationToken ct)
    {
        try
        {
            return await GitSystemTransport.RunAsync(path, arguments, Local, ct);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
