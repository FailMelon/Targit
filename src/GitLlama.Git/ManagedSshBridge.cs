using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace GitLlama.Git;

public sealed record SshBridgeRequest(string Host, int Port, string User, string Command);

internal sealed class ModernSshKey(Renci.SshNet.PrivateKeyFile key) : Renci.SshNet.IPrivateKeySource
{
    // Avoid SHA-1 RSA probes: some hosts disconnect instead of trying the next algorithm.
    public IReadOnlyCollection<Renci.SshNet.Security.HostAlgorithm> HostKeyAlgorithms { get; } = key.HostKeyAlgorithms.Where(algorithm => algorithm.Name != "ssh-rsa").ToArray();
}

/// <summary>Git's helper relays binary streams; private keys and passphrases never leave the parent process.</summary>
public sealed class ManagedSshBridge : IDisposable
{
    private readonly CancellationTokenSource _stop;
    private readonly NamedPipeServerStream _input, _output, _error;
    private int _disposed;
    public string PipeName { get; } = "GitLlama-ssh-" + Guid.NewGuid().ToString("N");
    public Task Completion { get; }
    public Exception? Failure { get; private set; }
    public bool Connected { get; private set; }
    public ManagedSshBridge(SshKeySession session, string keyPath, SshBridgeRequest endpoint, CancellationToken ct)
    {
        _stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _input = Pipe("-in", PipeDirection.In); _output = Pipe("-out", PipeDirection.Out); _error = Pipe("-err", PipeDirection.Out);
        Completion = Task.Run(() => ServeAsync(session, keyPath, endpoint, _stop.Token));
    }
    private NamedPipeServerStream Pipe(string suffix, PipeDirection direction) => new(PipeName + suffix, direction, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    private async Task ServeAsync(SshKeySession session, string keyPath, SshBridgeRequest endpoint, CancellationToken ct)
    {
        try
        {
            await Task.WhenAll(_input.WaitForConnectionAsync(ct), _output.WaitForConnectionAsync(ct), _error.WaitForConnectionAsync(ct)).ConfigureAwait(false);
            Connected = true;
            var size = new byte[4]; await _input.ReadExactlyAsync(size, ct).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32BigEndian(size);
            if (length is < 1 or > 16384) throw new InvalidOperationException("Invalid SSH bridge request.");
            var json = new byte[length]; await _input.ReadExactlyAsync(json, ct).ConfigureAwait(false);
            var request = JsonSerializer.Deserialize<SshBridgeRequest>(json) ?? throw new InvalidOperationException("Invalid SSH bridge request.");
            ValidateRequest(request, endpoint);
            var key = await session.UnlockAsync(keyPath, ct).ConfigureAwait(false);
            using var client = new SshClient(new ConnectionInfo(request.Host, request.Port, request.User, new PrivateKeyAuthenticationMethod(request.User, new ModernSshKey(key))) { Timeout = TimeSpan.FromSeconds(30) });
            Exception? trustFailure = null;
            client.HostKeyReceived += (_, args) =>
            {
                args.CanTrust = false;
                try { args.CanTrust = session.VerifyHostAsync(request.Host, request.Port, args.HostKeyName, args.HostKey, ct).GetAwaiter().GetResult(); }
                catch (Exception ex) { trustFailure = ex; }
            };
            try { await client.ConnectAsync(ct).ConfigureAwait(false); }
            catch (SshConnectionException ex) { if (trustFailure is not null) throw trustFailure; throw new InvalidOperationException("SSH connection or host verification failed. Check the server fingerprint and network connection.", ex); }
            using var command = client.CreateCommand(request.Command);
            command.CommandTimeout = Timeout.InfiniteTimeSpan;
            using var transferStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var execute = command.ExecuteAsync(ct);
            var sending = Task.Run(async () =>
            {
                using var stdin = command.CreateInputStream();
                try { await _input.CopyToAsync(stdin, transferStop.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            }, CancellationToken.None);
            var stdout = command.OutputStream.CopyToAsync(_output, ct);
            var stderr = command.ExtendedOutputStream.CopyToAsync(_error, ct);
            try { await execute.ConfigureAwait(false); await Task.WhenAll(stdout, stderr).ConfigureAwait(false); }
            finally { transferStop.Cancel(); _input.Dispose(); await sending.ConfigureAwait(false); }
            if (command.ExitStatus != 0) throw new InvalidOperationException("The SSH server rejected the Git operation. Check repository access and the remote URL.");
        }
        catch (Exception ex)
        {
            Failure = ex is SshAuthenticationException ? new InvalidOperationException("SSH key authentication failed. Register this key's public key with the account that has access to the repository.") : ex;
        }
        finally { _input.Dispose(); _output.Dispose(); _error.Dispose(); }
    }
    public static void ValidateRequest(SshBridgeRequest request, SshBridgeRequest expected)
    {
        if (!string.Equals(request.Host, expected.Host, StringComparison.OrdinalIgnoreCase) || request.Port != expected.Port || request.User != expected.User
            || !Regex.IsMatch(request.Command, @"^git-(?:upload-pack|receive-pack|upload-archive) .+$") || request.Command.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new InvalidOperationException("SSH helper requested an unexpected destination or command.");
    }
    public static SshBridgeRequest ParseArguments(IReadOnlyList<string> args)
    {
        var port = 22; var index = 0;
        while (index < args.Count && args[index].StartsWith('-'))
        {
            var option = args[index++];
            if (option is "-p" or "-P") { if (index == args.Count || !int.TryParse(args[index++], out port) || port is < 1 or > 65535) throw new InvalidOperationException("Invalid SSH port."); }
            else if (option == "-o") { if (index == args.Count || args[index++] != "SendEnv=GIT_PROTOCOL") throw new InvalidOperationException("Unsupported SSH option."); }
            else throw new InvalidOperationException("Unsupported SSH option.");
        }
        if (args.Count - index != 2) throw new InvalidOperationException("Invalid SSH helper arguments.");
        var destination = args[index]; var split = destination.LastIndexOf('@');
        if (split < 1) throw new InvalidOperationException("The SSH remote URL must include a username (for example git@host).");
        return new(destination[(split + 1)..], port, destination[..split], args[index + 1]);
    }
    public static SshBridgeRequest Endpoint(string url)
    {
        if (url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new(uri.Host, uri.Port > 0 ? uri.Port : 22, Uri.UnescapeDataString(uri.UserInfo), "");
        var match = Regex.Match(url, @"^(?<user>[^@\s]+)@(?<host>[^:\s]+):.+$");
        if (!match.Success) throw new InvalidOperationException("Enter an SSH repository URL with a username.");
        return new(match.Groups["host"].Value, 22, match.Groups["user"].Value, "");
    }
    public static async Task<int> RunHelperAsync(string pipeName, string[] args, Stream stdin, Stream stdout, Stream stderr, CancellationToken ct)
    {
        using var input = new NamedPipeClientStream(".", pipeName + "-in", PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var output = new NamedPipeClientStream(".", pipeName + "-out", PipeDirection.In, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var error = new NamedPipeClientStream(".", pipeName + "-err", PipeDirection.In, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await Task.WhenAll(input.ConnectAsync(30000, ct), output.ConnectAsync(30000, ct), error.ConnectAsync(30000, ct)).ConfigureAwait(false);
        var json = JsonSerializer.SerializeToUtf8Bytes(ParseArguments(args));
        var size = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(size, json.Length);
        await input.WriteAsync(size, ct).ConfigureAwait(false); await input.WriteAsync(json, ct).ConfigureAwait(false);
        _ = Task.Run(async () => { try { await stdin.CopyToAsync(input, ct).ConfigureAwait(false); } catch (Exception) { } finally { input.Dispose(); } });
        await Task.WhenAll(output.CopyToAsync(stdout, ct), error.CopyToAsync(stderr, ct)).ConfigureAwait(false);
        return 0; // The parent checks SSH command status before accepting Git's result.
    }
    public void Configure(ProcessStartInfo start, string helperCommand)
    {
        start.Environment.Remove("GIT_SSH"); start.Environment["GIT_SSH_VARIANT"] = "ssh";
        start.Environment["GIT_SSH_COMMAND"] = helperCommand + " --gitllama-ssh " + PipeName;
    }
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) != 0) return; _stop.Cancel(); _input.Dispose(); _output.Dispose(); _error.Dispose(); _stop.Dispose(); }
}
