namespace Targit.Infrastructure;

/// <summary>
/// Minimal cross-platform diagnostic log for startup, persistence and Git errors.
/// Logging failures must not prevent the application from running.
/// </summary>
public static class FileLog
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Targit", "targit.log");

    public static void Error(Exception exception, string context)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath,
                    $"[{DateTimeOffset.UtcNow:O}] ERROR {context}{Environment.NewLine}{exception}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // A full or read-only disk must not cause cascading failures.
        }
    }
}
