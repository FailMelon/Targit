namespace GitLlama.Infrastructure;

/// <summary>
/// Minimal cross-platform diagnostic log for startup, persistence and Git errors.
/// Logging failures must not prevent the application from running.
/// </summary>
public static class FileLog
{
    private static readonly object Gate = new();

    /// <summary>
    /// Destination of the log. Defaults to the user's application data folder;
    /// tests redirect it so they never write into the real user's log.
    /// </summary>
    public static string LogPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitLlama", "gitllama.log");

    public static void Error(Exception exception, string context)
    {
        try
        {
            lock (Gate)
            {
                var path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(
                    path,
                    SecretRedactor.Redact(
                        $"[{DateTimeOffset.UtcNow:O}] ERROR {context}{Environment.NewLine}{exception}{Environment.NewLine}"
                    )
                );
            }
        }
        catch (Exception)
        {
            // A full or read-only disk must not cause cascading failures.
        }
    }
}
