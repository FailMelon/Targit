using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Targit.Application;
using Targit.Core;
using Targit.Infrastructure;
namespace Targit.Desktop.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    private readonly RepositoryWorkspace _workspace;
    private readonly IRepositoryOverviewReader _overview;
    public ObservableCollection<string> CommitLines { get; } = [];
    public ObservableCollection<string> ChangeLines { get; } = [];
    public ObservableCollection<string> BranchLines { get; } = [];
    private readonly JsonSettingsStore _settings;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    public ObservableCollection<RepositoryInfo> Repositories { get; } = [];
    public ObservableCollection<string> Tabs { get; } = ["History", "Changes", "Branches", "Output"];
    public IReadOnlyList<string> LayoutNames { get; } = ["Standard", "Review", "Merge Resolution"];
    [ObservableProperty] private string selectedLayout = "Standard";
    public IReadOnlyList<string> AvailableTabs { get; } = ["History", "Changes", "Branches", "Output", "Worktrees", "Remotes", "Tags"];
    [ObservableProperty] private RepositoryInfo? selectedRepository;
    [ObservableProperty] private string selectedTab = "History";
    [ObservableProperty] private string currentBranch = "No repository";
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string repositoryPath = "";
    [ObservableProperty] private string newTab = "History";
    [ObservableProperty] private bool showLeftPanel = true;
    [ObservableProperty] private bool showRightPanel = true;
    public MainWindowViewModel(RepositoryWorkspace workspace, IRepositoryOverviewReader overview, JsonSettingsStore settings)
    {
        _workspace = workspace; _overview = overview; _settings = settings;
        _ = InitializeAsync();
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
            SelectedRepository = Repositories.FirstOrDefault(r => r.Path == data.ActiveRepository);
        }
        catch (Exception e) { Status = e.Message; }
    }
    partial void OnSelectedRepositoryChanged(RepositoryInfo? value) => _ = ActivateAsync(value);
    private async Task ActivateAsync(RepositoryInfo? repository)
    {
        if (repository is null) return;
        try
        {
            var snapshot = await _workspace.OpenAsync(repository.Path);
            if (SelectedRepository != repository) return;
            CurrentBranch = snapshot.Branch ?? "Detached HEAD";
            Status = $"Opened {repository.Name}";
            await RefreshAsync();
            await PersistAsync();
        }
        catch (Exception e) { Status = e.Message; }
    }
    partial void OnSelectedTabChanged(string value) => _ = PersistAsync();
    private async Task PersistAsync()
    {
        await _saveLock.WaitAsync();
        try { await _settings.SaveAsync(new TargitSettings(Repositories.ToList(), SelectedRepository?.Path, Tabs.ToList(), SelectedTab)); }
        catch (Exception e) { Status = e.Message; }
        finally { _saveLock.Release(); }
    }
    [RelayCommand]
    private async Task RefreshAsync()
    {
        var repo = SelectedRepository;
        if (repo is null) return;
        try
        {
            var overview = await _overview.ReadOverviewAsync(repo.Path);
            if (SelectedRepository != repo) return;
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
        catch (Exception e) { Status = e.Message; }
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
    private async Task AddRepositoryAsync()
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath)) return;
        try
        {
            var snapshot = await _workspace.OpenAsync(RepositoryPath);
            var existing = Repositories.FirstOrDefault(r => string.Equals(r.Path, snapshot.Path, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new RepositoryInfo(snapshot.Path, Path.GetFileName(Path.TrimEndingDirectorySeparator(snapshot.Path)));
                Repositories.Add(existing);
            }
            SelectedRepository = existing;
            RepositoryPath = "";
            await PersistAsync();
        }
        catch (Exception e) { Status = e.Message; }
    }
    [RelayCommand]
    private void AddTab()
    {
        if (!AvailableTabs.Contains(NewTab)) return;
        if (!Tabs.Contains(NewTab)) Tabs.Add(NewTab);
        SelectedTab = NewTab;
        _ = PersistAsync();
    }
    [RelayCommand]
    private void CloseTab()
    {
        if (Tabs.Count <= 1) return;
        Tabs.Remove(SelectedTab);
        SelectedTab = Tabs[0];
        _ = PersistAsync();
    }
    [RelayCommand]
    private void ToggleLeft() => ShowLeftPanel = !ShowLeftPanel;
    [RelayCommand]
    private void ToggleRight() => ShowRightPanel = !ShowRightPanel;
}