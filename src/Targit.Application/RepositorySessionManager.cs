using System.Collections.Concurrent;
using Targit.Core;

namespace Targit.Application;

/// <summary>
/// Owns per-repository refresh coordination without retaining native libgit2 handles.
/// A separate reader is free to open a short-lived handle for every operation.
/// </summary>
public sealed class RepositorySessionManager : IDisposable
{
    private readonly IRepositoryReader _reader;
    private readonly IRepositoryOverviewReader _overview;
    private readonly ConcurrentDictionary<string, RepositorySession> _sessions =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public RepositorySessionManager(IRepositoryReader reader, IRepositoryOverviewReader overview)
    {
        _reader = reader;
        _overview = overview;
    }

    public RepositorySession GetSession(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = Path.GetFullPath(path);
        return _sessions.GetOrAdd(normalized, p => new RepositorySession(p, _reader, _overview));
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values) session.Dispose();
        _sessions.Clear();
    }
}

public sealed class RepositorySession : IDisposable
{
    private readonly IRepositoryReader _reader;
    private readonly IRepositoryOverviewReader _overview;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _disposed;

    internal RepositorySession(string path, IRepositoryReader reader, IRepositoryOverviewReader overview)
    {
        Path = path;
        _reader = reader;
        _overview = overview;
    }

    public string Path { get; }
    public RepositorySnapshot? LastSnapshot { get; private set; }
    public RepositoryOverview? LastOverview { get; private set; }

    public async Task<RepositorySnapshot> OpenAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var snapshot = await _reader.ReadAsync(Path, ct);
        LastSnapshot = snapshot;
        return snapshot;
    }

    public async Task<RepositoryOverview> RefreshAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _refreshLock.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var overview = await _overview.ReadOverviewAsync(Path, ct);
            LastOverview = overview;
            return overview;
        }
        finally { _refreshLock.Release(); }
    }

    public void Dispose()
    {
        _disposed = true;
        // The manager is disposed after the desktop lifetime has ended.
        _refreshLock.Dispose();
    }
}
