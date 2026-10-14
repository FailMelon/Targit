using System.Security.Cryptography;
using System.Text;

namespace GitLlama.Git;

public sealed record SshPublicKey(string Text, string Type, string Fingerprint);

public static class SshPublicKeyReader
{
    public static async Task<SshPublicKey> ReadAsync(string path, CancellationToken ct = default)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > 32768)
        {
            throw new InvalidOperationException("Choose an OpenSSH public key file (.pub).");
        }

        return Parse(await File.ReadAllTextAsync(path, ct));
    }

    public static SshPublicKey Parse(string text)
    {
        var value = text.Trim();
        if (
            value.Contains("PRIVATE KEY", StringComparison.Ordinal)
            || value.StartsWith("PuTTY-User-Key-File", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "This is a private key. Choose its public .pub file; private key contents are never displayed or copied."
            );
        }

        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (
            parts.Length < 2
            || value.Contains('\n')
            || value.Contains('\r')
            || !(
                parts[0].StartsWith("ssh-", StringComparison.Ordinal)
                || parts[0].StartsWith("ecdsa-", StringComparison.Ordinal)
                || parts[0].StartsWith("sk-", StringComparison.Ordinal)
            )
        )
        {
            throw new InvalidOperationException("Choose a single-line OpenSSH public key file.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("The public key contains invalid Base64 data.");
        }

        if (bytes.Length < 8)
        {
            throw new InvalidOperationException("The public key is incomplete.");
        }

        var typeLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes);
        if (typeLength > bytes.Length - 4 || Encoding.ASCII.GetString(bytes, 4, (int)typeLength) != parts[0])
        {
            throw new InvalidOperationException("The public key type does not match its data.");
        }

        return new(value, parts[0], "SHA256:" + Convert.ToBase64String(SHA256.HashData(bytes)).TrimEnd('='));
    }
}
