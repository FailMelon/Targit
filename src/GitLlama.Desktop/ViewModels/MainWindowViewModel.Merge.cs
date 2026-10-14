using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitLlama.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>The Git operation that stopped part-way, such as "Merge" or "Rebase", or "None".</summary>
    [ObservableProperty]
    private string repositoryOperation = "None";

    /// <summary>The tab shown in the merge and conflict resolution document: Merge, Conflicts or Rebase.</summary>
    [ObservableProperty]
    private int mergeTabIndex;

    public int ConflictedFileCount => WorkingFiles.Count(file => file.Status == "Conflict");
    public bool HasMergeConflicts => ConflictedFileCount > 0;
    public bool IsMergeInProgress => RepositoryOperation == "Merge";
    public bool ShowMergeBanner => IsMergeInProgress || HasMergeConflicts;
    public bool CanFinishMerge => IsMergeInProgress && !HasMergeConflicts;
    public string MergeBannerText =>
        HasMergeConflicts
            ? (
                ConflictedFileCount == 1
                    ? "The merge stopped on 1 conflicted file. Resolve it, then finish the merge."
                    : $"The merge stopped on {ConflictedFileCount} conflicted files. Resolve them, then finish the merge."
            )
            : "All conflicts are resolved. Finish the merge to commit it.";

    partial void OnRepositoryOperationChanged(string value) => NotifyMergeState();

    private void NotifyMergeState()
    {
        OnPropertyChanged(nameof(ConflictedFileCount));
        OnPropertyChanged(nameof(HasMergeConflicts));
        OnPropertyChanged(nameof(IsMergeInProgress));
        OnPropertyChanged(nameof(ShowMergeBanner));
        OnPropertyChanged(nameof(CanFinishMerge));
        OnPropertyChanged(nameof(MergeBannerText));
        OnPropertyChanged(nameof(WorkingChangesCaption));
        ContinueMergeCommand.NotifyCanExecuteChanged();
        AbortMergeCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Opens the Conflicts tab with the current conflicts loaded and the first one selected.</summary>
    [RelayCommand]
    private async Task OpenConflictResolutionAsync()
    {
        MergeTabIndex = 1;
        DockDocument?.Invoke("Branches");
        await InspectMergeConflictsAsync();
        if (!MergeConflicts.Contains(ConflictPath))
        {
            ConflictPath = MergeConflicts.FirstOrDefault() ?? "";
        }
    }
}
