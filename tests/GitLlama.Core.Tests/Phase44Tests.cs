using LibGit2Sharp;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phase44Tests
{
    [Fact]
    public async Task InspectsCommonAncestorAndValidatesInput()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-44-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            string first;
            string second;
            using (var repo = new Repository(path))
            {
                var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "file.txt"), "first");
                Commands.Stage(repo, "file.txt");
                first = repo.Commit("first", signature, signature).Sha;
                File.WriteAllText(Path.Combine(path, "file.txt"), "second");
                Commands.Stage(repo, "file.txt");
                second = repo.Commit("second", signature, signature).Sha;
            }
            var service = new GitMergeBaseInspectionService();
            var result = await service.InspectAsync(path, first, second);
            Assert.Equal(first, result.BaseSha);
            Assert.Equal("first", result.BaseSubject);
            Assert.Equal(second, (await service.InspectAsync(path, second, second)).BaseSha);
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, " ", second));
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, first, " "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, new string('0', 40), second));
            await Assert.ThrowsAsync<ArgumentException>(() => service.InspectAsync(path, first, new string('0', 40)));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(path, true);
        }
    }
}