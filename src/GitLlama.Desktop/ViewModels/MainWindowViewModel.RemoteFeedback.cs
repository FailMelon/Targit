using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitLlama.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    [ObservableProperty] private string remoteOperationFeedback = "";
    public bool HasRemoteOperationFeedback => !string.IsNullOrWhiteSpace(RemoteOperationFeedback);
    partial void OnRemoteOperationFeedbackChanged(string value) => OnPropertyChanged(nameof(HasRemoteOperationFeedback));
    [RelayCommand] private void DismissRemoteFeedback() => RemoteOperationFeedback = "";
}
