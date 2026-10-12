using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Targit.Application;
using Targit.Core;
using Targit.Infrastructure;
namespace Targit.Desktop.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    private readonly RepositorySessionManager _sessions;
    public ObservableCollection<string> CommitLines { get; } = [];
    [ObservableProperty] private string? selectedCommitLine;
    [ObservableProperty] private string? selectedChangeLine;
    [ObservableProperty] private string? selectedBranchLine;
    [ObservableProperty] private string inspectorText = "Select a commit, changed file or branch to inspect it.";
    partial void OnSelectedCommitLineChanged(string? value) { if (value is not null) InspectorText = value; }
    partial void OnSelectedChangeLineChanged(string? value) { if (value is not null) InspectorText = value; }
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
    public ObservableCollection<RepositoryInfo> Repositories { get; } = [];
    public ObservableCollection<string> Tabs { get; } = ["History", "Changes", "Branches"];
    public IReadOnlyList<string> LayoutNames { get; } = ["Standard", "Review", "Merge Resolution"];
    [ObservableProperty] private string selectedLayout = "Standard";
    public IReadOnlyList<string> AvailableTabs { get; } = ["History", "Changes", "Branches"];
    [ObservableProperty] private RepositoryInfo? selectedRepository;
    [ObservableProperty] private string selectedTab = "History";
    [ObservableProperty] private string currentBranch = "No repository";
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string repositoryPath = "";
    [ObservableProperty] private string newTab = "History";
    public MainWindowViewModel(RepositorySessionManager sessions, JsonSettingsStore settings)
    {
        _sessions = sessions; _settings = settings;
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
        if (value is null)
        {
            CurrentBranch = "No repository";
            CommitLines.Clear(); ChangeLines.Clear(); BranchLines.Clear();
            InspectorText = "Select a repository to inspect it.";
        }
        else _ = ActivateAsync(value, version);
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
            if (version == Interlocked.Read(ref _repositorySelectionVersion)) await PersistAsync();
        }
        catch (Exception e) { FileLog.Error(e, "Open repository"); if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message; }
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
        try
        {
            var overview = await _sessions.GetSession(repo.Path).RefreshAsync();
            if (version != Interlocked.Read(ref _repositorySelectionVersion)) return;
            SelectedCommitLine = null;
            SelectedChangeLine = null;
            SelectedBranchLine = null;
            InspectorText = $"Repository: {repo.Name} · Branch: {CurrentBranch}";
            CommitLines.Clear();
            foreach (var commit in overview.Commits)
                CommitLines.Add($"{commit.Sha[..7]}  {commit.Message} — {commit.Author}");
            ChangeLines.Clear();
            foreach (var change in overview.Changes)
                ChangeLines.Add($"{change.State}  {change.Path}");
            BranchLines.Clear();
            foreach (var branch in overview.Branches) BranchLines.Add(branch);
            Status = $"{overview.Commits.Count} recent commits · {overview.Changes.Count} changed files";
        }
        catch (Exception e) { FileLog.Error(e, "Refresh repository"); if (version == Interlocked.Read(ref _repositorySelectionVersion)) Status = e.Message; }
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
}
