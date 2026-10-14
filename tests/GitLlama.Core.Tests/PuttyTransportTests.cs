using System.Diagnostics;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class PuttyTransportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GitLlama-PuTTY-" + Guid.NewGuid());

    private string FixtureFile(string name)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "transport fixture");
        return path;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PuttyKeySelectsPlinkWithPageantAndBatchMode(bool agent)
    {
        var key = FixtureFile("my key's $(echo unsafe).PPK");
        var plink = FixtureFile("PuTTY client.exe");
        var start = new ProcessStartInfo();
        start.Environment["GIT_SSH"] = "old-client";
        start.Environment["GIT_SSH_VARIANT"] = "ssh";
        GitSystemTransport.ConfigureSshEnvironment(
            start,
            new() { SshPrivateKeyPath = key, UseSshAgent = agent },
            () => plink
        );
        Assert.Equal("plink", start.Environment["GIT_SSH_VARIANT"]);
        Assert.False(start.Environment.ContainsKey("GIT_SSH"));
        var command = start.Environment["GIT_SSH_COMMAND"]!;
        Assert.Contains(" -ssh -batch -agent -i ", command);
        Assert.Contains("my key'\"'\"'s $(echo unsafe).PPK'", command);
        Assert.StartsWith("'", command);
        Assert.DoesNotContain("StrictHostKeyChecking", command);
    }

    [Fact]
    public async Task GitPassesQuotedKeyAndPlinkPortArgumentsToClient()
    {
        var key = FixtureFile("key's $(echo unsafe).ppk");
        var log = Path.Combine(directory, "arguments.txt");
        var client = FixtureFile("fake plink.sh");
        static string Quote(string path) => "'" + path.Replace('\\', '/').Replace("'", "'\"'\"'") + "'";
        File.WriteAllText(client, "#!/bin/sh\nprintf '%s\\n' \"$@\" > " + Quote(log) + "\nexit 1\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(client, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var start = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        start.ArgumentList.Add("ls-remote");
        start.ArgumentList.Add("ssh://git@example.invalid:2222/repo.git");
        GitSystemTransport.ConfigureSshEnvironment(
            start,
            new() { SshPrivateKeyPath = key, UseSshAgent = false },
            () => client
        );
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(timeout.Token);
        await output;
        Assert.True(File.Exists(log), await error);
        var arguments = File.ReadAllLines(log);
        Assert.Contains(key.Replace('\\', '/'), arguments);
        Assert.Contains("-P", arguments);
        Assert.Contains("2222", arguments);
        Assert.Contains("-batch", arguments);
        Assert.Contains("git@example.invalid", arguments);
    }

    [Fact]
    public void MissingPlinkExplainsInstallationWithoutConversion()
    {
        var key = FixtureFile("private.ppk");
        var error = Assert.Throws<InvalidOperationException>(() =>
            GitSystemTransport.ConfigureSshEnvironment(new(), new() { SshPrivateKeyPath = key }, () => null)
        );
        Assert.Contains("Install PuTTY", error.Message);
        Assert.Contains("does not need conversion", error.Message);
    }

    [Fact]
    public void MissingKeyIsRejectedBeforeRunningTransport()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GitSystemTransport.ConfigureSshEnvironment(
                new(),
                new() { SshPrivateKeyPath = Path.Combine(directory, "absent.ppk") },
                () => throw new Exception("Should not resolve client")
            )
        );
    }

    [Fact]
    public void OpenSshOverridesInheritedPlinkAndKeepsHostVerification()
    {
        var key = FixtureFile("id_ed25519");
        var start = new ProcessStartInfo();
        start.Environment["GIT_SSH_VARIANT"] = "plink";
        GitSystemTransport.ConfigureSshEnvironment(
            start,
            new() { SshPrivateKeyPath = key, UseSshAgent = false },
            () => throw new Exception("Should not use Plink")
        );
        Assert.Equal("ssh", start.Environment["GIT_SSH_VARIANT"]);
        Assert.Contains("StrictHostKeyChecking=yes -o IdentitiesOnly=yes -i ", start.Environment["GIT_SSH_COMMAND"]);
        GitSystemTransport.ConfigureSshEnvironment(start, new() { UseSshAgent = true });
        Assert.Equal("ssh -o BatchMode=yes -o StrictHostKeyChecking=yes", start.Environment["GIT_SSH_COMMAND"]);
    }

    [Theory]
    [InlineData("The host key is not cached for this server", "has not trusted")]
    [InlineData("The host key does not match the cached key", "fingerprint differs")]
    [InlineData("Cannot answer interactive prompts in batch mode: Passphrase for key", "Pageant")]
    [InlineData("FATAL ERROR: Cannot answer interactive prompts in batch mode", "Load key in Pageant")]
    [InlineData("Server refused our key", "authorized")]
    [InlineData("No supported authentication methods available", "Pageant")]
    public void PuttyFailuresHaveActionableMessages(string failure, string expected) =>
        Assert.Contains(expected, GitSystemTransport.ConnectionFailureMessage(failure, true));

    [Theory]
    [InlineData("git@github.com:owner/repo.git", "github.com", "git", "22")]
    [InlineData("ssh://git@ssh.example.com:2222/repo", "ssh.example.com", "git", "2222")]
    public void VerificationOpensOnlyRepositoryHost(string url, string host, string user, string port)
    {
        var start = PuttyHostVerification.CreateStartInfo(url, "putty.exe");
        Assert.Equal(new[] { "-ssh", "-P", port, "-l", user, host }, start.ArgumentList);
        Assert.False(start.UseShellExecute);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("git@-load:session")]
    [InlineData("ssh://git:password@github.com/repo")]
    [InlineData("ssh://git%20-pw%20secret@github.com/repo")]
    public void VerificationRejectsNonSshAndInjectedArguments(string url) =>
        Assert.Throws<InvalidOperationException>(() => PuttyHostVerification.CreateStartInfo(url, "putty.exe"));

    [Fact]
    public void VerificationSuppliesSelectedKeyAsOneArgumentAndEnablesPageant()
    {
        var key = FixtureFile("my key's $(echo unsafe).PPK");
        var start = PuttyHostVerification.CreateStartInfo("git@github.com:owner/repo.git", "putty.exe", key);
        Assert.Equal(new[] { "-ssh", "-P", "22", "-l", "git", "-agent", "-i", key, "github.com" }, start.ArgumentList);
        Assert.DoesNotContain("-batch", start.ArgumentList);
    }

    [Fact]
    public void VerificationRejectsMissingPuttyKey() =>
        Assert.Throws<InvalidOperationException>(() =>
            PuttyHostVerification.CreateStartInfo(
                "git@github.com:owner/repo.git",
                "putty.exe",
                Path.Combine(directory, "absent.ppk")
            )
        );

    [Fact]
    public void PageantReceivesOnlyTheSelectedKeyPath()
    {
        var key = FixtureFile("key's $(echo unsafe).ppk");
        var start = PuttyHostVerification.CreatePageantStartInfo("pageant.exe", key);
        Assert.Equal(new[] { key }, start.ArgumentList);
        Assert.False(start.UseShellExecute);
    }

    [Fact]
    public void PageantRejectsMissingOrWrongFormatKeys()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PuttyHostVerification.CreatePageantStartInfo("pageant.exe", FixtureFile("id_ed25519"))
        );
        Assert.Throws<InvalidOperationException>(() =>
            PuttyHostVerification.CreatePageantStartInfo("pageant.exe", Path.Combine(directory, "missing.ppk"))
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
