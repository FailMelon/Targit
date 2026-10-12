using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Targit.Application;
using Targit.Core;
using Targit.Infrastructure;
using Targit.Git;
namespace Targit.Desktop.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    private readonly RepositorySessionManager _sessions;
    private readonly GitWorkingTreeService _gitActions;
    private readonly GitDiffService _diffs = new();
    private readonly GitIdentityService _identity = new();
    private readonly GitTagService _tags = new();
    private readonly GitStashService _stashes = new();
    private readonly GitCommitAmendService _commitAmend = new();
    private readonly GitCherryPickService _cherryPick = new();
    private readonly GitRevertService _revert = new();
    private readonly GitSoftResetService _softReset = new();
    private readonly GitMixedResetService _mixedReset = new();
    private readonly GitHardResetService _hardReset = new();
    private readonly GitBranchFromCommitService _branchFromCommit = new();
    [ObservableProperty] private string branchFromCommitName = "";
    [ObservableProperty] private string hardResetConfirmation = "";
    [ObservableProperty] private string mixedResetConfirmation = "";
    [ObservableProperty] private string softResetConfirmation = "";
    [ObservableProperty] private string amendedCommitMessage = "";
    public ObservableCollection<GitStashInfo> Stashes { get; } = [];
    [ObservableProperty] private string stashMessage = "";
    [ObservableProperty] private GitStashInfo? selectedStash;
    [ObservableProperty] private string stashDropConfirmation = "";
    public ObservableCollection<GitTagInfo> Tags { get; } = [];
    [ObservableProperty] private string newTagName = "";
    [ObservableProperty] private string annotatedTagName = "";
    [ObservableProperty] private string annotatedTagMessage = "";
    [ObservableProperty] private string annotatedTagAuthorName = "";
    [ObservableProperty] private string annotatedTagAuthorEmail = "";
    [ObservableProperty] private GitTagInfo? selectedTag;
    [ObservableProperty] private string tagDeletionConfirmation = "";
    [ObservableProperty] private string remoteTagDeletionConfirmation = "";
    private readonly GitCommitInspectionService _commitInspection = new();
    private readonly Dictionary<string, GitCommitSummary> _commitDetails = new(StringComparer.Ordinal);
    [ObservableProperty] private string configuredIdentity = "No repository selected";
    [ObservableProperty] private string localIdentityName = "";
    [ObservableProperty] private string localIdentityEmail = "";
    private readonly GitBranchService _branches = new();
    [ObservableProperty] private string fileHistorySummary = "Select an action to inspect.";
    [ObservableProperty] private string fileHistoryPath = "";

    [ObservableProperty] private string graphLaneSummary = "Select an action to inspect.";

    [ObservableProperty] private string commitSearchSummary = "Select an action to inspect.";
    [ObservableProperty] private string commitSearchQuery = "";

    [ObservableProperty] private string graphLaneSummary = "Select an action to inspect.";
    private readonly GitCommitGraphService _commitGraph = new();
    [ObservableProperty] private string commitGraphSummary = "Load the commit graph to inspect parent relationships and refs.";
    private readonly GitRebaseConflictService _rebaseConflicts = new();
    [ObservableProperty] private string rebaseConflictSummary = "Select Inspect to view the current rebase state and unresolved paths.";
    private readonly GitRebasePlanService _rebasePlanner = new();
    [ObservableProperty] private string rebaseUpstreamSha = "";
    [ObservableProperty] private string rebasePlanPreview = "Enter an ancestor SHA to preview commits. No history will be rewritten.";
    private readonly GitRemoteService _remotes = new();
    public ObservableCollection<GitRemoteInfo> Remotes { get; } = [];
    public ObservableCollection<GitRemoteBranchInfo> RemoteBranches { get; } = [];
    [ObservableProperty] private GitRemoteBranchInfo? selectedRemoteBranch;
    [ObservableProperty] private GitRemoteInfo? selectedRemote;
    [ObservableProperty] private string newRemoteName = "";
    [ObservableProperty] private string newRemoteUrl = "";
    [ObservableProperty] private string upstreamBranchName = "";
    partial void OnSelectedRemoteChanged(GitRemoteInfo? value) => _ = LoadRemoteBranchesAsync(value);
    [ObservableProperty] private string newBranchName = "";
    [ObservableProperty] private string renameBranchName = "";
    private long _diffRevision;
    [ObservableProperty] private string stagedDiffText = "";
    [ObservableProperty] private string unstagedDiffText = "";
    private readonly Dictionary<string, string> _changePaths = new(StringComparer.Ordinal);
    public ObservableCollection<string> CommitLines { get; } = [];
    [ObservableProperty] private string? selectedCommitLine;
    [ObservableProperty] private string? selectedChangeLine;
    [ObservableProperty] private string? selectedBranchLine;
    [ObservableProperty] private string inspectorText = "Select a commit, changed file or branch to inspect it.";
    partial void OnSelectedCommitLineChanged(string? value)
    {
        if (value is not null && _commitDetails.TryGetValue(value, out var commit))
            InspectorText = $"Commit: {commit.Sha}\nAuthor: {commit.Author}\nDate: {commit.When:yyyy-MM-dd HH:mm:ss zzz}\nSubject: {commit.Message}";
        else if (value is not null) InspectorText = value;
    }
    partial void OnSelectedChangeLineChanged(string? value)
    {
        if (value is not null) InspectorText = value;
        _ = LoadSelectedDiffAsync(value);
    }
    private async Task LoadSelectedDiffAsync(string? selection)
    {
        var revision = Interlocked.Increment(ref _diffRevision);
        var repository = SelectedRepository;
        var selectionVersion = Interlocked.Read(ref _repositorySelectionVersion);
        StagedDiffText = "";
        UnstagedDiffText = "";
        if (repository is null || selection is null || !_changePaths.TryGetValue(selection, out var path)) return;
        try
        {
            var result = await _diffs.GetFileDiffAsync(repository.Path, path);
            if (revision != Interlocked.Read(ref _diffRevision) || selectionVersion != Interlocked.Read(ref _repositorySelectionVersion)) return;
            StagedDiffText = string.IsNullOrWhiteSpace(result.StagedPatch) ? "No staged changes for this file." : result.StagedPatch;
            UnstagedDiffText = string.IsNullOrWhiteSpace(result.UnstagedPatch) ? "No unstaged changes for this file." : result.UnstagedPatch;
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Load file diff");
            if (revision == Interlocked.Read(ref _diffRevision) && selectionVersion == Interlocked.Read(ref _repositorySelectionVersion)) UnstagedDiffText = e.Message;
        }
    }
    partial void OnSelectedBranchLineChanged(string? value) { if (value is not null) InspectorText = "Branch: " + value; }
    public ObservableCollection<string> ChangeLines { get; } = [];
    public ObservableCollection<string> BranchLines { get; } = [];
    private readonly JsonSettingsStore _settings;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    public Task Initialization { get; }
    private bool _initialized;
    private bool _loadingSettings = true;
    private long _repositorySelectionVersion;
    private long _settingsRevision;
    private long _refreshRevision;
    private readonly SemaphoreSlim _gitMutationLock = new(1, 1);
    public ObservableCollection<RepositoryInfo> Repositories { get; } = [];
    public ObservableCollection<string> Tabs { get; } = ["History", "Changes", "Branches", "Remotes"];
    public IReadOnlyList<string> LayoutNames { get; } = ["Standard", "Review", "Merge Resolution"];
    [ObservableProperty] private string selectedLayout = "Standard";
    public IReadOnlyList<string> AvailableTabs { get; } = ["History", "Changes", "Branches"];
    [ObservableProperty] private RepositoryInfo? selectedRepository;
    [ObservableProperty] private string selectedTab = "History";
    [ObservableProperty] private string currentBranch = "No repository";
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string repositoryPath = "";
    [ObservableProperty] private string newTab = "History";
    [ObservableProperty] private string stagingSummary = "No staged changes";
    [ObservableProperty] private string commitMessage = "";
    [ObservableProperty] private string authorName = "";
    [ObservableProperty] private string authorEmail = "";
    public MainWindowViewModel(RepositorySessionManager sessions, JsonSettingsStore settings, GitWorkingTreeService gitActions)
    {
        _sessions = sessions; _settings = settings; _gitActions = gitActions;
        Initialization = InitializeAsync();
    }
    private async Task InitializeAsync()
    {
        try
        {
            var data = await _settings.LoadAsync();
            foreach (var repository in data.Repositories) Repositories.Add(repository);
            if (data.Tabs is { Count: > 0 })
            {
                Tabs.Clear();
                foreach (var tab in data.Tabs.Distinct()) Tabs.Add(tab);
            }
            SelectedTab = Tabs.Contains(data.ActiveTab) ? data.ActiveTab : Tabs.FirstOrDefault() ?? "History";
            SelectedLayout = LayoutNames.Contains(data.LayoutName) ? data.LayoutName : "Standard";
            SelectedRepository = Repositories.FirstOrDefault(r => r.Path == data.ActiveRepository);
            _initialized = true;
        }
        catch (Exception e) { FileLog.Error(e, "Load settings"); Status = e.Message; }
        finally { _loadingSettings = false; _initialized = true; }
    }
    partial void OnSelectedRepositoryChanged(RepositoryInfo? value)
    {
        var version = Interlocked.Increment(ref _repositorySelectionVersion);
        SelectedChangeLine = null;
        _changePaths.Clear();
        ChangeLines.Clear();
        _commitDetails.Clear();
        Remotes.Clear();
        Tags.Clear();
        Stashes.Clear();
        SelectedStash = null;
        StashDropConfirmation = "";
        SelectedTag = null;
        TagDeletionConfirmation = "";
        RemoteTagDeletionConfirmation = "";
        SelectedRemote = null;
        StagingSummary = "No staged changes";
        ConfiguredIdentity = value is null ? "No repository selected" : "Loading Git identity...";
        LocalIdentityName = "";
        LocalIdentityEmail = "";
        if (value is null)
        {
            CurrentBranch = "No repository";
            CommitLines.Clear(); ChangeLines.Clear(); BranchLines.Clear();
            InspectorText = "Select a repository to inspect it.";
        }
        else _ = ActivateAsync(value, version);
        if (!_loadingSettings) _ = PersistAsync();
    }
    private async Task ActivateAsync(RepositoryInfo? repository, long version)
    {
        if (repository is null) return;
        try
        {
            var snapshot = await _sessions.GetSession(repository.Path).OpenAsync();
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            CurrentBranch = snapshot.Branch ?? "Detached HEAD";
            Status = $"Opened {repository.Name}";
            await RefreshAsync();
            await LoadIdentityAsync(repository.Path, version);
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) await PersistAsync();
        }
        catch (Exception e) { FileLog.Error(e, "Open repository"); if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message; }
    }
    private async Task LoadIdentityAsync(string path, long version)
    {
        try
        {
            var identity = await _identity.ReadAsync(path);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != path) return;
            LocalIdentityName = identity.Name ?? "";
            LocalIdentityEmail = identity.Email ?? "";
            ConfiguredIdentity = string.IsNullOrWhiteSpace(identity.Name) && string.IsNullOrWhiteSpace(identity.Email)
                ? "No Git identity configured"
                : $"{identity.Name ?? "(no name)"} <{identity.Email ?? "no email"}>";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Read Git identity");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) ConfiguredIdentity = "Unable to read Git identity: " + e.Message;
        }
    }
    [RelayCommand]
    private async Task SaveLocalIdentityAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var name = LocalIdentityName.Trim();
        var email = LocalIdentityEmail.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            Status = "Both author name and email are required.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await _identity.SetLocalAsync(repo.Path, name, email);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await LoadIdentityAsync(repo.Path, version);
            Status = "Saved repository-local Git identity; global Git settings were not changed.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Save local Git identity");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    partial void OnSelectedLayoutChanged(string value) { if (!_loadingSettings) _ = PersistAsync(); }
    partial void OnSelectedTabChanged(string value) { if (!_loadingSettings) _ = PersistAsync(); }
    private async Task PersistAsync()
    {
        if (!_initialized || _loadingSettings) return;
        var revision = Interlocked.Increment(ref _settingsRevision);
        await _saveLock.WaitAsync();
        try
        {
            if (revision != Interlocked.Read(ref _settingsRevision)) return;
            var snapshot = new TargitSettings(Repositories.ToList(), SelectedRepository?.Path, Tabs.ToList(), SelectedTab, LayoutName: SelectedLayout);
            await _settings.SaveAsync(snapshot);
        }
        catch (Exception e) { FileLog.Error(e, "Repository workspace operation"); Status = e.Message; }
        finally { _saveLock.Release(); }
    }
    [RelayCommand]
    private async Task RefreshAsync()
    {
        var repo = SelectedRepository;
        if (repo is null) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        var refreshVersion = Interlocked.Increment(ref _refreshRevision);
        try
        {
            var overview = await _sessions.GetSession(repo.Path).RefreshAsync();
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || refreshVersion != Interlocked.Read(ref _refreshRevision)) return;
            SelectedCommitLine = null;
            SelectedChangeLine = null;
            SelectedBranchLine = null;
            InspectorText = $"Repository: {repo.Name} · Branch: {CurrentBranch}";
            var recentCommits = await _commitInspection.ListRecentAsync(repo.Path);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || refreshVersion != Interlocked.Read(ref _refreshRevision)) return;
            CommitLines.Clear();
            _commitDetails.Clear();
            foreach (var commit in recentCommits)
            {
                var line = $"{commit.Sha[..7]}  {commit.Message} — {commit.Author}";
                CommitLines.Add(line);
                _commitDetails[line] = commit;
            }
            ChangeLines.Clear();
            _changePaths.Clear();
            foreach (var change in overview.Changes)
            {
                var line = $"{change.State}  {change.Path}";
                ChangeLines.Add(line);
                _changePaths[line] = change.Path;
            }
            var stagedCount = overview.Changes.Count(change =>
                change.State.Contains("InIndex", StringComparison.Ordinal));
            StagingSummary = stagedCount == 0
                ? "No staged changes"
                : stagedCount == 1 ? "1 staged file" : $"{stagedCount} staged files";
            BranchLines.Clear();
            foreach (var branch in overview.Branches) BranchLines.Add(branch);
            Status = $"{overview.Commits.Count} recent commits · {overview.Changes.Count} changed files";
            await RefreshRemotesAsync(repo.Path, version, refreshVersion);
            var tags = await _tags.ListAsync(repo.Path);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || refreshVersion != Interlocked.Read(ref _refreshRevision)) return;
            var previousTag = SelectedTag?.Name;
            Tags.Clear();
            foreach (var tag in tags) Tags.Add(tag);
            SelectedTag = Tags.FirstOrDefault(tag => tag.Name == previousTag);
            var stashes = await _stashes.ListAsync(repo.Path);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || refreshVersion != Interlocked.Read(ref _refreshRevision)) return;
            var previousStash = SelectedStash?.Message;
            Stashes.Clear();
            foreach (var stash in stashes) Stashes.Add(stash);
            SelectedStash = Stashes.FirstOrDefault(stash => stash.Message == previousStash);
        }
        catch (Exception e) { FileLog.Error(e, "Refresh repository"); if (version == Interlocked.Read(ref _repositorySelectionVersion) && refreshVersion == Interlocked.Read(ref _refreshRevision)) Status = e.Message; }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedRepository is null) return;
        var index = Repositories.IndexOf(SelectedRepository);
        if (index < 0) return;
        var replacement = SelectedRepository with { IsFavorite = !SelectedRepository.IsFavorite };
        Repositories[index] = replacement;
        SelectedRepository = replacement;
        await PersistAsync();
    }

    [RelayCommand]
    private async Task RemoveRepositoryAsync()
    {
        var selected = SelectedRepository;
        if (selected is null) return;
        Repositories.Remove(selected);
        SelectedRepository = Repositories.FirstOrDefault();
        if (SelectedRepository is null)
        {
            CurrentBranch = "No repository";
            CommitLines.Clear();
            ChangeLines.Clear();
            BranchLines.Clear();
            Status = "Repository removed from recent list (files were not deleted)";
        }
        await PersistAsync();
    }

    [RelayCommand]
    private async Task AddRepositoryAsync()
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath)) return;
        try
        {
            var snapshot = await _sessions.GetSession(RepositoryPath).OpenAsync();
            var existing = Repositories.FirstOrDefault(r => string.Equals(r.Path, snapshot.Path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (existing is null)
            {
                existing = new RepositoryInfo(snapshot.Path, Path.GetFileName(Path.TrimEndingDirectorySeparator(snapshot.Path)));
                Repositories.Add(existing);
            }
            SelectedRepository = existing;
            RepositoryPath = "";
            await PersistAsync();
        }
        catch (Exception e) { FileLog.Error(e, "Repository workspace operation"); Status = e.Message; }
    }
    private async Task RefreshRemotesAsync(string path, long version, long refreshVersion)
    {
        var remotes = await _remotes.ListAsync(path);
        if (version != Interlocked.Read(ref _repositorySelectionVersion) ||
            refreshVersion != Interlocked.Read(ref _refreshRevision)) return;
        var selectedName = SelectedRemote?.Name;
        Remotes.Clear();
        foreach (var remote in remotes) Remotes.Add(remote);
        SelectedRemote = remotes.FirstOrDefault(remote => remote.Name == selectedName);
        if (SelectedRemote is not null) await LoadRemoteBranchesAsync(SelectedRemote);
    }

    [RelayCommand]
    private async Task AddRemoteAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var name = NewRemoteName.Trim();
        var url = NewRemoteUrl.Trim();
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await _remotes.AddAsync(repo.Path, name, url);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            NewRemoteName = "";
            NewRemoteUrl = "";
            await RefreshAsync();
            Status = $"Added remote {name}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Add remote");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task FetchRemoteAsync()
    {
        if (SelectedRepository is not { } repo || SelectedRemote is not { } remote) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            Status = $"Fetching {remote.Name}...";
            await _remotes.FetchAsync(repo.Path, remote.Name);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await RefreshAsync();
            Status = $"Fetched {remote.Name}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Fetch remote");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task PushRemoteAsync()
    {
        if (SelectedRepository is not { } repo || SelectedRemote is not { } remote) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            Status = $"Pushing current branch to {remote.Name}...";
            var branch = await _remotes.PushCurrentBranchAsync(repo.Path, remote.Name);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await RefreshAsync();
            Status = $"Pushed {branch} to {remote.Name}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Push remote");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    private async Task LoadRemoteBranchesAsync(GitRemoteInfo? remote)
    {
        var repo = SelectedRepository;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        RemoteBranches.Clear();
        SelectedRemoteBranch = null;
        if (repo is null || remote is null) return;
        try
        {
            var branches = await _remotes.ListRemoteBranchesAsync(repo.Path, remote.Name);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)
                || SelectedRemote?.Name != remote.Name) return;
            foreach (var branch in branches) RemoteBranches.Add(branch);
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Load remote branches");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
    }

    partial void OnSelectedRemoteBranchChanged(GitRemoteBranchInfo? value)
    {
        if (value is not null) UpstreamBranchName = value.BranchName;
    }

    [RelayCommand]
    private async Task CheckoutRemoteBranchAsync()
    {
        if (SelectedRepository is not { } repo || SelectedRemoteBranch is not { } remoteBranch) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            var localName = remoteBranch.BranchName;
            await _branches.CheckoutRemoteBranchAsync(repo.Path, remoteBranch.RemoteName,
                remoteBranch.BranchName, localName);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            CurrentBranch = localName;
            await RefreshAsync();
            Status = $"Checked out {localName} tracking {remoteBranch.RemoteName}/{remoteBranch.BranchName}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Checkout remote branch");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task SetUpstreamAsync()
    {
        if (SelectedRepository is not { } repo || SelectedRemote is not { } remote) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await _remotes.SetCurrentBranchUpstreamAsync(repo.Path, remote.Name, UpstreamBranchName.Trim());
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await RefreshAsync();
            Status = $"Tracking {remote.Name}/{UpstreamBranchName.Trim()}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Set upstream");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task ClearUpstreamAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await _remotes.ClearCurrentBranchUpstreamAsync(repo.Path);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await RefreshAsync();
            Status = "Current branch upstream cleared";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Clear upstream");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task PullFastForwardAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            Status = "Pulling tracked branch (fast-forward only)...";
            await _remotes.PullFastForwardAsync(repo.Path);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await RefreshAsync();
            Status = "Pull complete";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Pull fast-forward");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task RemoveRemoteAsync()
    {
        if (SelectedRepository is not { } repo || SelectedRemote is not { } remote) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await _remotes.RemoveAsync(repo.Path, remote.Name);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await RefreshAsync();
            Status = $"Removed remote {remote.Name}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Remove remote");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task PushSelectedTagAsync()
    {
        if (SelectedRepository is not { } repo || SelectedRemote is not { } remote || SelectedTag is not { } tag) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await _tags.PushAsync(repo.Path, remote.Name, tag.Name);
            if (version == Interlocked.Read(ref _repositorySelectionVersion))
                Status = $"Pushed tag {tag.Name} to {remote.Name} (without force).";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Push tag");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task DeleteSelectedRemoteTagAsync()
    {
        if (SelectedRepository is not { } repo || SelectedRemote is not { } remote || SelectedTag is not { } tag) return;
        var expected = $"{remote.Name}/{tag.Name}";
        if (!string.Equals(RemoteTagDeletionConfirmation.Trim(), expected, StringComparison.Ordinal))
        {
            Status = $"Type {expected} to confirm remote tag deletion.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await _tags.DeleteRemoteAsync(repo.Path, remote.Name, tag.Name);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            RemoteTagDeletionConfirmation = "";
            Status = $"Deleted remote tag {tag.Name} from {remote.Name}; local tag preserved.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Delete remote tag");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task DeleteSelectedTagAsync()
    {
        if (SelectedRepository is not { } repo || SelectedTag is not { } tag) return;
        var tagName = tag.Name;
        if (!string.Equals(TagDeletionConfirmation.Trim(), tagName, StringComparison.Ordinal))
        {
            Status = $"Type {tagName} to confirm local tag deletion.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await _tags.DeleteAsync(repo.Path, tagName);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            SelectedTag = null;
            TagDeletionConfirmation = "";
            await RefreshAsync();
            Status = $"Deleted local tag {tagName}; remote tags were not changed.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Delete local tag");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task PopSelectedStashAsync()
    {
        if (SelectedRepository is not { } repo || SelectedStash is not { } stash) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var current = await _stashes.ListAsync(repo.Path);
            if (stash.Index >= current.Count || current[stash.Index].Message != stash.Message)
                throw new InvalidOperationException("The selected stash changed. Refresh and select it again.");
            await _stashes.PopAsync(repo.Path, stash.Index);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            SelectedStash = null;
            await RefreshAsync();
            Status = $"Popped stash@{{{stash.Index}}}; entry removed after successful application.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Pop stash");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task ApplySelectedStashAsync()
    {
        if (SelectedRepository is not { } repo || SelectedStash is not { } stash) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var current = await _stashes.ListAsync(repo.Path);
            if (stash.Index >= current.Count || current[stash.Index].Message != stash.Message)
                throw new InvalidOperationException("The selected stash changed. Refresh and select it again.");
            await _stashes.ApplyAsync(repo.Path, stash.Index);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await RefreshAsync();
            Status = $"Applied stash@{{{stash.Index}}}; stash entry was preserved.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Apply stash");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task DropSelectedStashAsync()
    {
        if (SelectedRepository is not { } repo || SelectedStash is not { } stash) return;
        var expected = $"stash@{{{stash.Index}}}";
        if (!string.Equals(StashDropConfirmation.Trim(), expected, StringComparison.Ordinal))
        {
            Status = $"Type {expected} to confirm permanent stash deletion.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var current = await _stashes.ListAsync(repo.Path);
            if (stash.Index >= current.Count || current[stash.Index].Message != stash.Message)
                throw new InvalidOperationException("The selected stash changed. Refresh and select it again.");
            await _stashes.DropAsync(repo.Path, stash.Index);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            SelectedStash = null;
            StashDropConfirmation = "";
            await RefreshAsync();
            Status = $"Dropped {expected} permanently without applying it.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Drop stash");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task SaveTrackedStashAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var message = StashMessage.Trim();
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await _stashes.SaveAsync(repo.Path, message);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            StashMessage = "";
            await RefreshAsync();
            Status = "Saved tracked changes to stash (untracked files excluded).";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Save tracked stash");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task CreateBranchFromSelectedCommitAsync()
    {
        if (SelectedRepository is not { } repo || SelectedCommitLine is not { } line ||
            !_commitDetails.TryGetValue(line, out var commit))
        {
            Status = "Select a commit in History to create a branch from.";
            return;
        }
        var name = BranchFromCommitName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Status = "Enter a name for the new branch.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var created = await _branchFromCommit.CreateAsync(repo.Path, commit.Sha, name);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            BranchFromCommitName = "";
            await RefreshAsync();
            Status = $"Created branch {created} at {commit.Sha[..7]} without switching branches.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Create branch from commit");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task HardResetToSelectedCommitAsync()
    {
        if (SelectedRepository is not { } repo || SelectedCommitLine is not { } line ||
            !_commitDetails.TryGetValue(line, out var commit))
        {
            Status = "Select an ancestor commit in History.";
            return;
        }
        var expected = $"HARD RESET {commit.Sha[..7]}";
        if (!string.Equals(HardResetConfirmation.Trim(), expected, StringComparison.Ordinal))
        {
            Status = $"Type {expected} to confirm discarding commits and resetting tracked files.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var sha = await _hardReset.ResetToAncestorAsync(repo.Path, commit.Sha);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            HardResetConfirmation = "";
            await RefreshAsync();
            Status = $"Hard reset to {sha[..7]}; tracked files and index reset to target.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Hard reset");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task MixedResetToSelectedCommitAsync()
    {
        if (SelectedRepository is not { } repo || SelectedCommitLine is not { } line ||
            !_commitDetails.TryGetValue(line, out var commit))
        {
            Status = "Select an ancestor commit in History.";
            return;
        }
        var expected = $"MIXED {commit.Sha[..7]}";
        if (!string.Equals(MixedResetConfirmation.Trim(), expected, StringComparison.Ordinal))
        {
            Status = $"Type {expected} to confirm moving HEAD and unstaging changes.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var sha = await _mixedReset.ResetToAncestorAsync(repo.Path, commit.Sha);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            MixedResetConfirmation = "";
            await RefreshAsync();
            Status = $"Mixed reset HEAD to {sha[..7]}; index reset, working files preserved.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Mixed reset");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task SoftResetToSelectedCommitAsync()
    {
        if (SelectedRepository is not { } repo || SelectedCommitLine is not { } line ||
            !_commitDetails.TryGetValue(line, out var commit))
        {
            Status = "Select an ancestor commit in History.";
            return;
        }
        var expected = $"RESET {commit.Sha[..7]}";
        if (!string.Equals(SoftResetConfirmation.Trim(), expected, StringComparison.Ordinal))
        {
            Status = $"Type {expected} to confirm moving HEAD.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var sha = await _softReset.ResetToAncestorAsync(repo.Path, commit.Sha);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            SoftResetConfirmation = "";
            await RefreshAsync();
            Status = $"Soft reset HEAD to {sha[..7]}; index and working files preserved.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Soft reset");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task RevertSelectedCommitAsync()
    {
        if (SelectedRepository is not { } repo || SelectedCommitLine is not { } line ||
            !_commitDetails.TryGetValue(line, out var commit))
        {
            Status = "Select a commit in History to revert.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var sha = await _revert.RevertAsync(repo.Path, commit.Sha);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await RefreshAsync();
            Status = $"Reverted {commit.Sha[..7]} with new commit {sha[..7]}.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Revert commit");
            if (version == Interlocked.Read(ref _repositorySelectionVersion))
            {
                await RefreshAsync();
                Status = e.Message;
            }
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task CherryPickSelectedCommitAsync()
    {
        if (SelectedRepository is not { } repo || SelectedCommitLine is not { } line ||
            !_commitDetails.TryGetValue(line, out var commit))
        {
            Status = "Select a commit in History to cherry-pick.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var sha = await _cherryPick.CherryPickAsync(repo.Path, commit.Sha);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await RefreshAsync();
            Status = $"Cherry-picked {commit.Sha[..7]} as {sha[..7]}.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Cherry-pick commit");
            if (version == Interlocked.Read(ref _repositorySelectionVersion))
            {
                await RefreshAsync();
                Status = e.Message;
            }
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task AmendLatestCommitMessageAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var message = AmendedCommitMessage.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            Status = "Enter a replacement commit message.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var sha = await _commitAmend.AmendLatestMessageAsync(repo.Path, message);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            AmendedCommitMessage = "";
            await RefreshAsync();
            Status = $"Amended latest unpublished commit message: {sha[..7]}.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Amend commit message");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task CreateAnnotatedTagAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var tagName = AnnotatedTagName.Trim();
        var message = AnnotatedTagMessage.Trim();
        var authorName = AnnotatedTagAuthorName.Trim();
        var authorEmail = AnnotatedTagAuthorEmail.Trim();
        if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(message) ||
            string.IsNullOrWhiteSpace(authorName) || string.IsNullOrWhiteSpace(authorEmail))
        {
            Status = "Annotated tags require a name, message, author name and author email.";
            return;
        }
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await _tags.CreateAnnotatedAsync(repo.Path, tagName, message, authorName, authorEmail);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            AnnotatedTagName = "";
            AnnotatedTagMessage = "";
            await RefreshAsync();
            Status = $"Created annotated tag {tagName}.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Create annotated tag");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task CreateLightweightTagAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var tagName = NewTagName.Trim();
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            await _tags.CreateAsync(repo.Path, tagName);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            NewTagName = "";
            await RefreshAsync();
            Status = $"Created lightweight tag {tagName}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Create lightweight tag");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task PreviewRebasePlanAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        try
        {
            var steps = await _rebasePlanner.PlanAsync(repo.Path, RebaseUpstreamSha.Trim());
            GitRebasePlanService.ValidatePlan(steps);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            RebasePlanPreview = steps.Count == 0 ? "No commits to rebase." :
                string.Join(Environment.NewLine, steps.Select(step => $"pick {step.CommitSha[..7]} {step.Subject}"));
            Status = $"Previewed {steps.Count} rebase steps. No commits were modified.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Preview rebase plan");
            if (version == Interlocked.Read(ref _repositorySelectionVersion))
            {
                RebasePlanPreview = e.Message;
                Status = e.Message;
            }
        }
    }

    [RelayCommand]
    private async Task InspectRebaseConflictsAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        try
        {
            var state = await _rebaseConflicts.InspectAsync(repo.Path);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            RebaseConflictSummary = (state.IsRebasing ? "Rebase in progress" : "No rebase in progress") +
                Environment.NewLine + (state.Paths.Count == 0 ? "No unresolved index conflicts." :
                "Unresolved paths:" + Environment.NewLine + string.Join(Environment.NewLine, state.Paths));
            Status = state.Paths.Count == 0 ? "No unresolved index conflicts." : $"{state.Paths.Count} unresolved conflicted paths.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Inspect rebase conflicts");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = RebaseConflictSummary = e.Message;
        }
    }

    [RelayCommand]
    private async Task LoadCommitGraphAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        try
        {
            var nodes = await _commitGraph.GetAsync(repo.Path);
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            CommitGraphSummary = nodes.Count == 0 ? "No commits yet." :
                string.Join(Environment.NewLine, nodes.Select(node =>
                    $"{node.Sha[..7]} {node.Subject} | parents: {(node.Parents.Count == 0 ? "root" : string.Join(", ", node.Parents.Select(parent => parent[..7])))} | {string.Join(", ", node.Labels)}"));
            Status = $"Loaded {nodes.Count} commit graph nodes.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Load commit graph");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = CommitGraphSummary = e.Message;
        }
    }

    [RelayCommand]
    private async Task LoadGraphLanesAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        try
        {
            string output;
            var lanes = await new GitGraphLaneService().GetAsync(repo.Path); output = lanes.Count == 0 ? "No commits." : string.Join(Environment.NewLine, lanes.Select(lane => $"lane {lane.Lane}: {lane.Sha[..7]} -> {string.Join(", ", lane.Parents.Select(parent => parent[..7]))}"));
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            GraphLaneSummary = output;
            Status = "Load graph lanes completed.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Load graph lanes");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = GraphLaneSummary = e.Message;
        }
    }

    [RelayCommand]
    private async Task SearchCommitsAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        try
        {
            string output;
            var hits = await new GitCommitSearchService().SearchAsync(repo.Path, CommitSearchQuery.Trim()); output = hits.Count == 0 ? "No matching commits." : string.Join(Environment.NewLine, hits.Select(hit => $"{hit.Sha[..7]} {hit.Subject} — {hit.Author}"));
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            CommitSearchSummary = output;
            Status = "Search commits completed.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Search commits");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = CommitSearchSummary = e.Message;
        }
    }

    [RelayCommand]
    private async Task LoadFileHistoryAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        try
        {
            string output;
            var entries = await new GitFileHistoryService().GetAsync(repo.Path, FileHistoryPath.Trim()); output = entries.Count == 0 ? "No file history." : string.Join(Environment.NewLine, entries.Select(entry => $"{entry.Sha[..7]} {entry.Subject}"));
            if (version != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            FileHistorySummary = output;
            Status = "Load file history completed.";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Load file history");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = FileHistorySummary = e.Message;
        }
    }

    [RelayCommand]
    private async Task CreateBranchAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var name = NewBranchName.Trim();
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await _branches.CreateAsync(repo.Path, name);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            NewBranchName = "";
            await RefreshAsync();
            Status = $"Created branch {name}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Create branch");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task CheckoutBranchAsync()
    {
        if (SelectedRepository is not { } repo || SelectedBranchLine is not { } selected) return;
        var name = selected.Trim();
        if (name.StartsWith("* ", StringComparison.Ordinal)) name = name[2..];
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            var current = await _branches.CheckoutAsync(repo.Path, name);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            CurrentBranch = current;
            await RefreshAsync();
            Status = $"Switched to {current}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Checkout branch");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task RenameBranchAsync()
    {
        if (SelectedRepository is not { } repo || SelectedBranchLine is not { } selected) return;
        var oldName = selected.Trim();
        if (oldName.StartsWith("* ", StringComparison.Ordinal)) oldName = oldName[2..];
        var newName = RenameBranchName.Trim();
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await _branches.RenameAsync(repo.Path, oldName, newName);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            if (CurrentBranch == oldName) CurrentBranch = newName;
            RenameBranchName = "";
            await RefreshAsync();
            Status = $"Renamed branch {oldName} to {newName}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Rename branch");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task DeleteBranchAsync()
    {
        if (SelectedRepository is not { } repo || SelectedBranchLine is not { } selected) return;
        var name = selected.Trim();
        if (name.StartsWith("* ", StringComparison.Ordinal)) name = name[2..];
        var version = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await _branches.DeleteAsync(repo.Path, name);
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            await RefreshAsync();
            Status = $"Deleted branch {name}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Delete branch");
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task StageSelectedAsync()
    {
        await ChangeStageAsync(stage: true);
    }

    [RelayCommand]
    private async Task UnstageSelectedAsync()
    {
        await ChangeStageAsync(stage: false);
    }

    private async Task ChangeStageAsync(bool stage)
    {
        if (SelectedRepository is not { } repo || SelectedChangeLine is not { } selected ||
            !_changePaths.TryGetValue(selected, out var path)) return;
        var selectionVersion = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (selectionVersion != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            if (stage) await _gitActions.StageAsync(repo.Path, path);
            else await _gitActions.UnstageAsync(repo.Path, path);
            if (SelectedRepository?.Path != repo.Path) return;
            await RefreshAsync();
            if (SelectedRepository?.Path == repo.Path) Status = stage ? $"Staged {path}" : $"Unstaged {path}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, stage ? "Stage file" : "Unstage file");
            if (SelectedRepository?.Path == repo.Path) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }

    [RelayCommand]
    private async Task CommitAsync()
    {
        if (SelectedRepository is not { } repo) return;
        var message = CommitMessage;
        var author = AuthorName;
        var email = AuthorEmail;
        var selectionVersion = Interlocked.Read(ref _repositorySelectionVersion);
        await _gitMutationLock.WaitAsync();
        try
        {
            if (selectionVersion != Interlocked.Read(ref _repositorySelectionVersion) || SelectedRepository?.Path != repo.Path) return;
            var sha = await _gitActions.CommitAsync(repo.Path, message, author, email);
            if (SelectedRepository?.Path != repo.Path) return;
            CommitMessage = "";
            await RefreshAsync();
            if (SelectedRepository?.Path == repo.Path) Status = $"Created commit {sha[..7]}";
        }
        catch (Exception e)
        {
            FileLog.Error(e, "Create commit");
            if (SelectedRepository?.Path == repo.Path) Status = e.Message;
        }
        finally { _gitMutationLock.Release(); }
    }
}
