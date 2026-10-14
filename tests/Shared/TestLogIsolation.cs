using System.Runtime.CompilerServices;
using GitLlama.Infrastructure;

namespace GitLlama.Tests.Shared;

/// <summary>Keeps diagnostics written during tests out of the real user's application log.</summary>
internal static class TestLogIsolation
{
    [ModuleInitializer]
    internal static void RedirectFileLog() =>
        FileLog.LogPath = Path.Combine(
            Path.GetTempPath(),
            "GitLlama-Tests",
            typeof(TestLogIsolation).Assembly.GetName().Name + ".log"
        );
}
