using System.Diagnostics;
using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class RemoteBranchEditingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "GitLlama-remote-edit-" + Guid.NewGuid());
    private readonly GitRemoteService service = new();
    private string Clone => Path.Combine(root, "clone");
    private string Remote => Path.Combine(root, "remote.git");
    public RemoteBranchEditingTests()
    {
        Directory.CreateDirectory(root); var seed = Path.Combine(root, "seed"); Repository.Init(seed);
        using (var repo = new Repository(seed)) { File.WriteAllText(Path.Combine(seed, "a.txt"), "initial"); Commands.Stage(repo, "a.txt"); var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow); repo.Commit("initial", signature, signature); }
        Repository.Clone(seed, Remote, new CloneOptions { IsBare = true }); Repository.Clone(Remote, Clone);
    }
    [Fact]
    public async Task CreateRenameAndDeleteRemoteBranchPreserveLocalBranchAndMoveTracking()
    {
        var source = await service.PrepareBranchAsync(Clone, "origin", null);
        await service.ApplyBranchAsync(source, RemoteBranchOperation.Create, "feature/new", new());
        using (var remote = new Repository(Remote)) Assert.Equal(source.CommitSha, remote.Branches["feature/new"].Tip.Sha);
        using (var local = new Repository(Clone)) { var branch = local.CreateBranch("local-feature"); local.Branches.Update(branch, updater => updater.TrackedBranch = "refs/remotes/origin/feature/new"); }
        var rename = await service.PrepareBranchAsync(Clone, "origin", "feature/new");
        await service.ApplyBranchAsync(rename, RemoteBranchOperation.Rename, "feature/renamed", new());
        using (var remote = new Repository(Remote)) { Assert.Null(remote.Branches["feature/new"]); Assert.Equal(source.CommitSha, remote.Branches["feature/renamed"].Tip.Sha); }
        using (var local = new Repository(Clone)) Assert.Equal("origin/feature/renamed", local.Branches["local-feature"].TrackedBranch.FriendlyName);
        await service.ApplyBranchAsync(await service.PrepareBranchAsync(Clone, "origin", "feature/renamed"), RemoteBranchOperation.Delete, "", new());
        using (var remote = new Repository(Remote)) Assert.Null(remote.Branches["feature/renamed"]);
        using (var local = new Repository(Clone)) Assert.NotNull(local.Branches["local-feature"]);
    }
    [Fact]
    public async Task RenameRejectsExistingDestinationAtomically()
    {
        var draft = await service.PrepareBranchAsync(Clone, "origin", null);
        await service.ApplyBranchAsync(draft, RemoteBranchOperation.Create, "old", new());
        await service.ApplyBranchAsync(draft, RemoteBranchOperation.Create, "existing", new());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyBranchAsync(draft with { BranchName = "old" }, RemoteBranchOperation.Rename, "existing", new()));
        Assert.Contains("already exists", error.Message);
        using var remote = new Repository(Remote); Assert.NotNull(remote.Branches["old"]); Assert.NotNull(remote.Branches["existing"]);
    }
    [Fact]
    public async Task DeleteRejectsBranchThatMovedAfterPreview()
    {
        var draft = await service.PrepareBranchAsync(Clone, "origin", null);
        await service.ApplyBranchAsync(draft, RemoteBranchOperation.Create, "feature", new());
        var preview = await service.PrepareBranchAsync(Clone, "origin", "feature");
        string moved;
        using (var repo = new Repository(Clone)) { File.WriteAllText(Path.Combine(Clone, "b.txt"), "new"); Commands.Stage(repo, "b.txt"); var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.UtcNow); moved = repo.Commit("new", signature, signature).Sha; repo.Network.Push(repo.Network.Remotes["origin"], "HEAD:refs/heads/feature"); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyBranchAsync(preview, RemoteBranchOperation.Delete, "", new()));
        using var remote = new Repository(Remote); Assert.Equal(moved, remote.Branches["feature"].Tip.Sha);
    }
    [Fact]
    public async Task EditRemoteUrlPreservesNameAndRejectsStaleConfiguration()
    {
        var original = (await service.ListAsync(Clone)).Single();
        await service.EditAsync(Clone, original, "https://github.com/example/new.git", "git@github.com:example/new.git");
        var updated = (await service.ListAsync(Clone)).Single(); Assert.Equal("origin", updated.Name); Assert.Equal("git@github.com:example/new.git", updated.PushUrl);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EditAsync(Clone, original, Remote, Remote));
    }
    [Fact]
    public void HttpsTokenStaysInHostScopedProcessEnvironment()
    {
        var start = new ProcessStartInfo("git"); var options = new GitNetworkOptions { CredentialHost = "github.com", Username = "x-access-token", Secret = "test-token" };
        GitSystemTransport.ConfigureHttpsTokenEnvironment(start, options, "https://github.com/owner/repo.git");
        Assert.Contains("http.https://github.com/", start.Environment["GIT_CONFIG_KEY_1"]);
        Assert.Equal("false", start.Environment["GIT_CONFIG_VALUE_3"]); Assert.Empty(start.ArgumentList);
        Assert.Throws<InvalidOperationException>(() => GitSystemTransport.ConfigureHttpsTokenEnvironment(start, options, "https://other.example/repo"));
    }
    public void Dispose() { foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal); Directory.Delete(root, true); }
}
