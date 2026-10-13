using CommunityToolkit.Mvvm.ComponentModel;
using GitLlama.Core;

namespace GitLlama.Desktop.ViewModels;

public partial class RepositoryWorkspaceTab : ObservableObject
{
    [ObservableProperty] private RepositoryInfo? repository;
    public string Title => Repository?.Name ?? "New repository";
    public string Tooltip => Repository?.Path ?? "Open, clone or initialize a repository";
    public bool IsEmpty => Repository is null;
    partial void OnRepositoryChanged(RepositoryInfo? value)
    {
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Tooltip)); OnPropertyChanged(nameof(IsEmpty));
    }
}
