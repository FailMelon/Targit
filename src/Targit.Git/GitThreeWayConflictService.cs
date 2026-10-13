using System.Text;
using LibGit2Sharp;

namespace Targit.Git;

public sealed record GitThreeWayConflict(string Path, string? Base, string? Ours, string? Theirs);

/// <summary>Reads the three index stages without modifying conflicted files.</summary>
public sealed class GitThreeWayConflictService
{
    public Task<GitThreeWayConflict> InspectAsync(string repositoryPath, string relativePath,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        using var repo = new Repository(repositoryPath);
        var entry = repo.Index.Conflicts[relativePath]
            ?? throw new InvalidOperationException("The path is not conflicted.");
        string? Read(IndexEntry? index)
        {
            if (index is null) return null;
            var blob = repo.Lookup<Blob>(index.Id)
                ?? throw new InvalidOperationException("Conflict stage blob is missing.");
            if (blob.Size > 1_000_000)
                throw new InvalidOperationException("Conflict stage exceeds the 1 MB preview limit.");
            using var stream = blob.GetContentStream();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            if (Array.IndexOf(bytes, (byte)0) >= 0)
                throw new InvalidOperationException("Binary conflict stages cannot be previewed.");
            cancellationToken.ThrowIfCancellationRequested();
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        return new GitThreeWayConflict(relativePath, Read(entry.Ancestor), Read(entry.Ours), Read(entry.Theirs));
    }, cancellationToken);
}
