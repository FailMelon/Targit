using System.Diagnostics;
using LibGit2Sharp;

namespace GitLlama.Git;

public static class GitSystemTransport
{
    public static bool IsSsh(string url) => url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) || (!Path.IsPathRooted(url) && url.Contains('@') && url.Contains(':') && !url.Contains("://"));
    public static async Task<string> RunAsync(string? repositoryPath, IEnumerable<string> arguments, GitNetworkOptions options, CancellationToken ct, string? input = null, bool ssh = false)
    {
        ct.ThrowIfCancellationRequested();
        options.Progress?.Report(new("Connecting through system Git", 0, 0));
        var start = new ProcessStartInfo("git") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (!string.IsNullOrEmpty(repositoryPath)) { start.ArgumentList.Add("-C"); start.ArgumentList.Add(repositoryPath); }
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GCM_INTERACTIVE"] = "true";
        foreach (var trace in new[] { "GIT_TRACE", "GIT_TRACE_CURL", "GIT_CURL_VERBOSE", "GCM_TRACE", "GCM_TRACE_SECRETS" }) start.Environment.Remove(trace);
        if (!ssh && !string.IsNullOrWhiteSpace(options.AzurePersonalAccessToken)) ConfigureAzurePatEnvironment(start, options);
        if (ssh && !options.UseSshAgent)
        {
            if (string.IsNullOrWhiteSpace(options.SshPrivateKeyPath)) throw new InvalidOperationException("Choose an OpenSSH private key, or enable the local SSH agent.");
            var key = Path.GetFullPath(options.SshPrivateKeyPath);
            if (!File.Exists(key) || key.EndsWith(".ppk", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose an existing OpenSSH private key. PuTTY .ppk keys need conversion first.");
            // Git invokes this through its shell; quote the path as one literal argument.
            var quoted = "'" + key.Replace("\\", "/").Replace("'", "'\"'\"'") + "'";
            start.Environment["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes -o StrictHostKeyChecking=yes -o IdentitiesOnly=yes -i " + quoted;
        }
        else if (ssh) start.Environment["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes -o StrictHostKeyChecking=yes";
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (System.ComponentModel.Win32Exception) { throw new InvalidOperationException("Install Git with Git Credential Manager and OpenSSH, then restart GitLlama."); }
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        using var registration = ct.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        if (input is not null) await process.StandardInput.WriteAsync(input.AsMemory(), ct).ConfigureAwait(false);
        process.StandardInput.Close();
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var result = await output.ConfigureAwait(false); var failure = await error.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var message = failure.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase) ? "SSH host verification failed. Verify the server fingerprint and add the host to OpenSSH known_hosts before connecting." : failure.Contains("Permission denied (publickey", StringComparison.OrdinalIgnoreCase) ? "SSH key authentication failed. Load an authorized key into your agent, or choose the matching OpenSSH private key." : "Git connection failed. Check account access, the configured credential helper, or SSH keys and known_hosts.";
            throw new InvalidOperationException(message);
        }
        options.Progress?.Report(new("Git operation completed", 0, 0));
        return result;
    }
    public static void ConfigureAzurePatEnvironment(ProcessStartInfo start, GitNetworkOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AzurePersonalAccessToken) || options.AzurePersonalAccessToken.IndexOfAny(['\r', '\n']) >= 0 || !Uri.TryCreate(options.AzureCredentialScopeUrl, UriKind.Absolute, out var scope) || scope.Scheme != "https" || !scope.IsDefaultPort || !string.IsNullOrEmpty(scope.UserInfo) || !string.IsNullOrEmpty(scope.Query) || !string.IsNullOrEmpty(scope.Fragment) || !scope.AbsolutePath.EndsWith('/')) throw new InvalidOperationException("Invalid Azure DevOps credential scope.");
        var org = scope.Host == "dev.azure.com" ? scope.AbsolutePath.Trim('/') : scope.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase) ? scope.Host[..^17] : "";
        if (!GitLlama.Core.AzureDevOpsRemote.ValidOrganization(org)) throw new InvalidOperationException("Invalid Azure DevOps credential scope.");
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_CONFIG_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        start.Environment["GIT_CONFIG_COUNT"] = "4";
        start.Environment["GIT_CONFIG_KEY_0"] = "http." + options.AzureCredentialScopeUrl + ".extraheader";
        start.Environment["GIT_CONFIG_VALUE_0"] = "Authorization: Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(":" + options.AzurePersonalAccessToken));
        start.Environment["GIT_CONFIG_KEY_1"] = "http.followRedirects"; start.Environment["GIT_CONFIG_VALUE_1"] = "false";
        start.Environment["GIT_CONFIG_KEY_2"] = "credential.helper"; start.Environment["GIT_CONFIG_VALUE_2"] = "";
        start.Environment["GIT_CONFIG_KEY_3"] = "http.sslVerify"; start.Environment["GIT_CONFIG_VALUE_3"] = "true";
        start.Environment["GCM_INTERACTIVE"] = "never";
    }
    public static async Task<UsernamePasswordCredentials> GetCredentialsAsync(string url, string? repositoryPath, string username, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidOperationException("Use an HTTPS URL without embedded credentials.");
        if (username.Contains('\n') || username.Contains('\r')) throw new ArgumentException("Invalid username.");
        var request = $"protocol=https\nhost={uri.Authority}\npath={Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))}\n";
        if (request.Contains('\r') || Uri.UnescapeDataString(uri.AbsolutePath).Contains('\n')) throw new ArgumentException("Invalid repository URL.");
        if (!string.IsNullOrWhiteSpace(username)) request += "username=" + username + "\n";
        var result = await RunAsync(repositoryPath, ["credential", "fill"], new GitNetworkOptions(), ct, request + "\n").ConfigureAwait(false);
        return ParseCredentials(result, uri.Authority);
    }
    public static UsernamePasswordCredentials ParseCredentials(string response, string expectedAuthority)
    {
        var fields = response.Split('\n').Where(line => line.Contains('=')).Select(line => line.TrimEnd('\r').Split('=', 2)).GroupBy(parts => parts[0]).ToDictionary(group => group.Key, group => group.Last()[1]);
        if (fields.GetValueOrDefault("protocol") != "https" || !string.Equals(fields.GetValueOrDefault("host"), expectedAuthority, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(fields.GetValueOrDefault("username")) || string.IsNullOrEmpty(fields.GetValueOrDefault("password")))
            throw new InvalidOperationException("The credential helper did not return credentials for this HTTPS host.");
        return new UsernamePasswordCredentials { Username = fields["username"], Password = fields["password"] };
    }
    public static async Task ForgetCredentialsAsync(string url, string? repositoryPath, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidOperationException("Enter an HTTPS repository URL without embedded credentials.");
        var request = $"protocol=https\nhost={uri.Authority}\npath={Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))}\n\n";
        if (Uri.UnescapeDataString(uri.AbsolutePath).IndexOfAny(['\r', '\n']) >= 0) throw new ArgumentException("Invalid repository URL.");
        await RunAsync(repositoryPath, ["credential", "reject"], new GitNetworkOptions(), ct, request).ConfigureAwait(false);
    }
    public static async Task CheckConnectionAsync(string url, string? repositoryPath, GitNetworkOptions options, CancellationToken ct)
    {
        options.EnsureAzureAccess(url);
        if (IsSsh(url)) { await RunAsync(repositoryPath, ["ls-remote", "--", url, "HEAD"], options, ct, ssh: true).ConfigureAwait(false); return; }
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
        {
            if (options.UsesAzureAuthorization(url)) { await RunAsync(repositoryPath, ["ls-remote", "--", url, "HEAD"], options, ct).ConfigureAwait(false); return; }
            await GetCredentialsAsync(url, repositoryPath, options.Username, ct).ConfigureAwait(false);
            await RunAsync(repositoryPath, ["ls-remote", "--", url, "HEAD"], options, ct).ConfigureAwait(false); return;
        }
        throw new InvalidOperationException("Choose an HTTPS or SSH repository URL.");
    }
}
