using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class ConflictDocumentTests
{
    [Fact]
    public void ParsesSharedLinesAndConflictsIncludingTheBaseSection()
    {
        var segments = GitConflictDocumentService.Parse(
            "top\n<<<<<<< HEAD\nours 1\nours 2\n||||||| base\nold\n=======\ntheirs\n>>>>>>> feature\nbottom\n"
        );

        Assert.Equal(3, segments.Count);
        Assert.Equal(["top"], segments[0].Common);
        Assert.True(segments[1].IsConflict);
        Assert.Equal(["ours 1", "ours 2"], segments[1].Ours);
        Assert.Equal(["theirs"], segments[1].Theirs);
        Assert.Equal(["bottom"], segments[2].Common);
    }

    [Fact]
    public void KeepsAnUnterminatedConflictAsText()
    {
        var segments = GitConflictDocumentService.Parse("a\n<<<<<<< HEAD\nb\n");

        Assert.Equal(["a"], segments[0].Common);
        Assert.Equal(["b"], segments[^1].Common);
        Assert.DoesNotContain(segments, segment => segment.IsConflict);
    }

    [Fact]
    public async Task ReadsAConflictedMergeAndMarksResolvedFilesOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitllama-conflict-doc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Repository.Init(path);
            using (var repo = new Repository(path))
            {
                var sig = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow);
                File.WriteAllText(Path.Combine(path, "notes.txt"), "title\nnight 5\nend\n");
                File.WriteAllText(Path.Combine(path, "other.txt"), "base\n");
                Commands.Stage(repo, "*");
                repo.Commit("root", sig, sig);
                var source = repo.Head.FriendlyName;
                repo.CreateBranch("incoming");
                File.WriteAllText(Path.Combine(path, "notes.txt"), "title\nnight 6\nend\n");
                File.WriteAllText(Path.Combine(path, "other.txt"), "ours\n");
                Commands.Stage(repo, "*");
                repo.Commit("ours", sig, sig);
                Commands.Checkout(repo, repo.Branches["incoming"]);
                File.WriteAllText(Path.Combine(path, "notes.txt"), "title\nnight 8\nend\n");
                File.WriteAllText(Path.Combine(path, "other.txt"), "theirs\n");
                Commands.Stage(repo, "*");
                repo.Commit("theirs", sig, sig);
                Commands.Checkout(repo, repo.Branches[source]);
            }

            Assert.Equal(MergeStatus.Conflicts, await new GitMergeService().MergeAsync(path, "incoming"));
            var document = await new GitConflictDocumentService().ReadAsync(path, "notes.txt");
            Assert.Equal(1, document.ConflictCount);
            Assert.Equal("incoming", document.Theirs.Name);
            Assert.Equal(7, document.Ours.ShortSha.Length);
            Assert.Equal(["title"], document.Segments[0].Common);
            Assert.Equal(["night 6"], document.Segments[1].Ours);
            Assert.Equal(["night 8"], document.Segments[1].Theirs);
            Assert.Equal(["end"], document.Segments[2].Common);

            File.WriteAllText(Path.Combine(path, "notes.txt"), "title\nnight 7\nend\n");
            var stillMarked = await new GitConflictResolutionService().MarkAllResolvedAsync(path);
            Assert.Equal(["other.txt"], stillMarked);
            Assert.Equal(["other.txt"], (await new GitConflictDetectionService().ListAsync(path)).Select(c => c.Path));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(path, true);
        }
    }
}
