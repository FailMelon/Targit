using GitLlama.Git;
using GitLlama.Tests.Shared;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

[Collection("Git LFS")]
public sealed class GitLfsLockTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "GitLlama-LfsLocks-" + Guid.NewGuid().ToString("N")
    );
    private readonly string _path;
    private readonly FakeLfsLockServer _server = new();
    private readonly GitLfsService _lfs = new();
    private readonly GitNetworkOptions _network = new();

    public GitLfsLockTests()
    {
        GitLfsFilter.EnsureRegistered();
        _path = Path.Combine(_root, "Game");
        Directory.CreateDirectory(_path);
        Repository.Init(_path);
        using var repo = new Repository(_path);
        repo.Config.Set("user.name", "Test");
        repo.Config.Set("user.email", "test@example.com");
        repo.Config.Set("lfs.url", _server.Url);
    }

    [Fact]
    public void ParsesOursAndTheirs()
    {
        var locks = GitLfsService.ParseLocks(
            """
            {"ours":[{"id":"1","path":"Art/Hero.psd","owner":{"name":"Me"},"locked_at":"2026-10-14T10:00:00Z"}],
             "theirs":[{"id":"2","path":"Audio/Theme.wav","owner":{"name":"Alex"},"locked_at":"2026-10-13T09:00:00Z"}],
             "next_cursor":""}
            """
        );

        Assert.Equal(
            [
                new GitLfsLock("1", "Art/Hero.psd", "Me", DateTimeOffset.Parse("2026-10-14T10:00:00Z"), true),
                new GitLfsLock("2", "Audio/Theme.wav", "Alex", DateTimeOffset.Parse("2026-10-13T09:00:00Z"), false),
            ],
            locks
        );
    }

    [Fact]
    public async Task LocksAndUnlocksFilesOnTheServer()
    {
        await CommitLockableFileAsync();
        _server.Seed("Audio/Theme.wav", "Alex");

        await _lfs.LockAsync(_path, "Art/Hero.psd", null, _network);
        var locks = await _lfs.GetLocksAsync(_path, null, _network);
        Assert.Contains(locks, entry => entry is { Path: "Art/Hero.psd", IsOurs: true });
        Assert.Contains(locks, entry => entry is { Path: "Audio/Theme.wav", Owner: "Alex", IsOurs: false });

        await _lfs.UnlockAsync(_path, "Art/Hero.psd", force: false, null, _network);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _lfs.UnlockAsync(_path, "Audio/Theme.wav", force: false, null, _network)
        );
        await _lfs.UnlockAsync(_path, "Audio/Theme.wav", force: true, null, _network);

        Assert.Empty(await _lfs.GetLocksAsync(_path, null, _network));
    }

    [Fact]
    public async Task LockableFilesAreReadOnlyUntilLocked()
    {
        await CommitLockableFileAsync();
        var file = Path.Combine(_path, "Art", "Hero.psd");

        await _lfs.ApplyLockablePermissionsAsync(_path);
        Assert.True(new FileInfo(file).IsReadOnly);

        await _lfs.LockAsync(_path, "Art/Hero.psd", null, _network);
        Assert.False(new FileInfo(file).IsReadOnly);

        await _lfs.UnlockAsync(_path, "Art/Hero.psd", force: false, null, _network);
        Assert.True(new FileInfo(file).IsReadOnly);
    }

    [Fact]
    public async Task TracksWhichPatternsAreLockable()
    {
        await _lfs.TrackAsync(_path, "*.psd");
        await _lfs.TrackAsync(_path, "*.wav");
        await _lfs.SetLockableAsync(_path, "*.psd", lockable: true);
        Assert.Equal(["*.psd"], (await _lfs.GetStatusAsync(_path)).LockablePatterns);

        await _lfs.SetLockableAsync(_path, "*.psd", lockable: false);
        Assert.Empty((await _lfs.GetStatusAsync(_path)).LockablePatterns);
    }

    [Fact]
    public async Task FindsUnpushedChangesToFilesLockedBySomeoneElse()
    {
        await CommitLockableFileAsync();
        using var repo = new Repository(_path);
        // Pretend the first commit is already on the remote.
        repo.Refs.Add("refs/remotes/origin/master", repo.Head.Tip.Id);
        Directory.CreateDirectory(Path.Combine(_path, "Audio"));
        File.WriteAllText(Path.Combine(_path, "Audio", "Theme.wav"), "new audio");
        File.SetAttributes(Path.Combine(_path, "Art", "Hero.psd"), FileAttributes.Normal);
        File.WriteAllText(Path.Combine(_path, "Art", "Hero.psd"), "edited art");
        Commands.Stage(repo, "*");
        Commit(repo, "Edit art and add audio");

        var locks = new[]
        {
            new GitLfsLock("1", "Art/Hero.psd", "Alex", DateTimeOffset.UtcNow, false),
            new GitLfsLock("2", "Docs/Plan.psd", "Alex", DateTimeOffset.UtcNow, false),
            new GitLfsLock("3", "Audio/Theme.wav", "Me", DateTimeOffset.UtcNow, true),
        };

        var conflict = Assert.Single(GitLfsService.LockConflicts(repo, "origin", locks));
        Assert.Equal("Art/Hero.psd", conflict.Path);
    }

    public void Dispose()
    {
        GitLfsFilter.Shutdown();
        _server.Dispose();
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, true);
    }

    private async Task CommitLockableFileAsync()
    {
        await _lfs.TrackAsync(_path, "*.psd");
        await _lfs.SetLockableAsync(_path, "*.psd", lockable: true);
        Directory.CreateDirectory(Path.Combine(_path, "Art"));
        File.WriteAllText(Path.Combine(_path, "Art", "Hero.psd"), "art");
        using var repo = new Repository(_path);
        Commands.Stage(repo, "*");
        Commit(repo, "Add art");
    }

    private static void Commit(Repository repo, string message)
    {
        var signature = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
        repo.Commit(message, signature, signature);
    }
}
