using System.Text;
using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitCommitFileContent(string CommitSha, string Path, string Content, long Size);

public sealed class GitCommitFileContentService
{
    public Task<GitCommitFileContent> GetAsync(string repositoryPath, string commitSha, string filePath,
        int maxBytes = 1_000_000, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            if (maxBytes is < 1 or > 10_000_000) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            cancellationToken.ThrowIfCancellationRequested();
            using var repository = new Repository(repositoryPath);
            var commit = repository.Lookup<Commit>(commitSha.Trim())
                ?? throw new ArgumentException("Commit not found.", nameof(commitSha));
            var path = filePath.Trim().Replace('\\', '/');
            var entry = commit.Tree[path]
                ?? throw new ArgumentException("File not found in commit.", nameof(filePath));
            if (entry.Target is not Blob blob)
                throw new ArgumentException("Path does not refer to a file.", nameof(filePath));
            if (blob.Size > maxBytes)
                throw new InvalidOperationException($"File exceeds the {maxBytes} byte preview limit.");
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = blob.GetContentStream();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            if (Array.IndexOf(bytes, (byte)0) >= 0)
                throw new InvalidOperationException("Binary files cannot be previewed as text.");
            cancellationToken.ThrowIfCancellationRequested();
            return new GitCommitFileContent(commit.Sha, path,
                new UTF8Encoding(false, true).GetString(bytes), blob.Size);
        }, cancellationToken);
}