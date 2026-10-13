namespace GitLlama.Infrastructure;

/// <summary>Debounced local file notifications. No Git work is performed on the watcher thread.</summary>
public sealed class RepositoryWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Timer _timer;
    private readonly object _gate = new();
    private bool _disposed;
    public RepositoryWatcher(IEnumerable<string> roots, Action changed)
    {
        _timer = new Timer(_ => { lock (_gate) { if (_disposed) return; } changed(); }, null, Timeout.Infinite, Timeout.Infinite);
        foreach (var root in roots.Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
        {
            if (!Directory.Exists(root)) continue;
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 32 * 1024
            };
            watcher.Changed += (_, _) => Schedule(); watcher.Created += (_, _) => Schedule();
            watcher.Deleted += (_, _) => Schedule(); watcher.Renamed += (_, _) => Schedule();
            watcher.Error += (_, _) => Schedule();
            _watchers.Add(watcher); watcher.EnableRaisingEvents = true;
        }
    }
    private void Schedule() { lock (_gate) { if (!_disposed) _timer.Change(500, Timeout.Infinite); } }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _timer.Dispose();
            foreach (var watcher in _watchers) watcher.Dispose(); _watchers.Clear();
        }
    }
}
