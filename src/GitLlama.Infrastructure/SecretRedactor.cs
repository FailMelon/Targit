using System.Text.RegularExpressions;

namespace GitLlama.Infrastructure;

public static partial class SecretRedactor
{
    [GeneratedRegex(@"(https?://)[^\s/@]+:[^\s/@]+@", RegexOptions.IgnoreCase)] private static partial Regex UrlCredentials();
    [GeneratedRegex(@"\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+)\b")] private static partial Regex GitHubTokens();
    [GeneratedRegex(@"(?i)(authorization\s*[:=]\s*(?:bearer|basic)\s+|(?:access_token|token|password|secret)\s*[=:]\s*)[^\s&]+")]
    private static partial Regex NamedSecrets();
    public static string Redact(string text) => NamedSecrets().Replace(GitHubTokens().Replace(UrlCredentials().Replace(text, "$1[redacted]@"), "[redacted]"), "$1[redacted]");
}
