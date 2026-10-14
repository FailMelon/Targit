using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitLlama.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    private bool _changingScopeForBranchReveal;
    private long _branchRevealRevision;

    [ObservableProperty]
    private long historyRevealRevision;

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RevealBranchHistoryAsync(string? branch)
    {
        if (branch is null || SelectedRepository is null || IsUpdatingContextSnapshot)
        {
            return;
        }

        var revision = Interlocked.Increment(ref _branchRevealRevision);
        var repositoryVersion = Interlocked.Read(ref _repositorySelectionVersion);
        bool IsCurrent() =>
            revision == Interlocked.Read(ref _branchRevealRevision)
            && repositoryVersion == Interlocked.Read(ref _repositorySelectionVersion);
        SelectedBranchLine = branch;
        DockDocument?.Invoke("History");
        CenterSection = SelectedTab = "History";
        IsFileReview = false;
        IsWorkingTreeInspector = false;
        HistorySearch = HistoryAuthorFilter = "";
        HistorySince = HistoryUntil = null;
        var reset = SelectedHistoryScope != "All branches" || _loadedGraphNodes.Count == 0;
        _changingScopeForBranchReveal = true;
        try
        {
            SelectedHistoryScope = "All branches";
        }
        finally
        {
            _changingScopeForBranchReveal = false;
        }

        if (reset)
        {
            await ReadHistoryPageAsync(true);
        }

        while (IsCurrent() && !HistoryRows.Any(row => row.Labels.Contains(branch)) && HasMoreHistory)
        {
            var count = _loadedGraphNodes.Count;
            await ReadHistoryPageAsync(false);
            if (count == _loadedGraphNodes.Count)
            {
                break;
            }
        }

        if (!IsCurrent())
        {
            return;
        }

        var tip = HistoryRows.FirstOrDefault(row => row.Labels.Contains(branch));
        if (tip is null)
        {
            Status = $"No loaded commit found for {branch}.";
            return;
        }

        SelectedHistoryRow = tip;
        HistoryRevealRevision++;
    }
}
