using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GitLlama.Git;

public static class PuttyHostVerification
{
    public static ProcessStartInfo CreatePageantStartInfo(string pageantPath, string privateKeyPath)
    {
        if (!privateKeyPath.EndsWith(".ppk", StringComparison.OrdinalIgnoreCase) || !File.Exists(privateKeyPath))
            throw new InvalidOperationException("Choose an existing PuTTY private key (.ppk) in SSH settings first.");
        var start = new ProcessStartInfo(pageantPath) { UseShellExecute = false };
        start.ArgumentList.Add(Path.GetFullPath(privateKeyPath));
        return start;
    }
    public static ProcessStartInfo CreateStartInfo(string url, string puttyPath, string? privateKeyPath = null)
    {
        string host, user;
        var port = 22;
        if (url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
            user = Uri.UnescapeDataString(uri.UserInfo);
            if (uri.Port > 0) port = uri.Port;
        }
        else
        {
            var match = Regex.Match(url, @"^(?<user>[A-Za-z0-9_.-]+)@(?<host>[A-Za-z0-9_.-]+):.+$");
            if (!match.Success) throw new InvalidOperationException("Enter an SSH repository URL to verify its host.");
            host = match.Groups["host"].Value;
            user = match.Groups["user"].Value;
        }
        if (!Regex.IsMatch(host, @"^[A-Za-z0-9][A-Za-z0-9.:-]*$") || (user.Length > 0 && !Regex.IsMatch(user, @"^[A-Za-z0-9_][A-Za-z0-9_.-]*$")) || port is < 1 or > 65535)
            throw new InvalidOperationException("Enter an SSH URL with a valid host, username and port.");
        var start = new ProcessStartInfo(puttyPath) { UseShellExecute = false };
        start.ArgumentList.Add("-ssh");
        start.ArgumentList.Add("-P");
        start.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (user.Length > 0) { start.ArgumentList.Add("-l"); start.ArgumentList.Add(user); }
        if (!string.IsNullOrWhiteSpace(privateKeyPath) && privateKeyPath.EndsWith(".ppk", StringComparison.OrdinalIgnoreCase))
        {
            var key = Path.GetFullPath(privateKeyPath);
            if (!File.Exists(key)) throw new InvalidOperationException("Choose an existing PuTTY private key (.ppk) before opening PuTTY.");
            start.ArgumentList.Add("-agent");
            start.ArgumentList.Add("-i");
            start.ArgumentList.Add(key);
        }
        start.ArgumentList.Add(host);
        return start;
    }
}
