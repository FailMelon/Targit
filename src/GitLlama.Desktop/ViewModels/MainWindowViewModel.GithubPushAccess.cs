using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitLlama.Core;
using GitLlama.Providers;

namespace GitLlama.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    [ObservableProperty]
    private string githubPushAccessUrl = "";

    [ObservableProperty]
    private bool needsGithubPrivateAccess;

    [ObservableProperty]
    private bool needsGithubWorkflowAccess;

    [ObservableProperty]
    private bool isGithubIntegrationExpanded;
    public bool HasGithubPushAccessLink => GithubPushAccessUrl.Length > 0;

    partial void OnGithubPushAccessUrlChanged(string value) => OnPropertyChanged(nameof(HasGithubPushAccessLink));

    internal Func<
        HostingRepository,
        string,
        CancellationToken,
        Task<GitHubPushAccess>
    > GithubPushAccessCheck { get; set; } =
        async (repo, token, ct) =>
        {
            using var checker = new GitHubPushAccessChecker();
            return await checker.CheckAsync(repo, token, ct);
        };

    private void ClearGithubPushAccessFeedback()
    {
        GithubPushAccessUrl = "";
        NeedsGithubPrivateAccess = false;
        NeedsGithubWorkflowAccess = false;
    }

    internal void OfferGithubAccessAfterPushFailure(string url, string token, string message)
    {
        if (
            _githubConnection is null
            || !IsGithubConnected
            || string.IsNullOrEmpty(token)
            || !string.IsNullOrWhiteSpace(NetworkSecret)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || !uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo)
            || GitHubProvider.FromRemote(url) is null
        )
        {
            return;
        }

        if (
            message.Contains("workflow", StringComparison.OrdinalIgnoreCase)
            && message.Contains("scope", StringComparison.OrdinalIgnoreCase)
        )
        {
            NeedsGithubWorkflowAccess = true;
            Status = RemoteOperationFeedback =
                "GitHub blocked this push because it includes GitHub Actions workflow files. Reconnect GitHub and authorize the workflow permission, then retry the push.";
            return;
        }

        if (
            !new[]
            {
                "401",
                "403",
                "404",
                "authentication",
                "permission denied",
                "access denied",
                "not found",
                "denied to",
            }.Any(part => message.Contains(part, StringComparison.OrdinalIgnoreCase))
        )
        {
            return;
        }

        GithubPushAccessUrl =
            "https://github.com/settings/connections/applications/" + Uri.EscapeDataString(_githubConnection.ClientId);
        Status = RemoteOperationFeedback +=
            " Review GitLlama's authorization and organization or SSO access on GitHub.";
    }

    internal async Task<bool> CheckLinkedGithubPushAccessAsync(string url, string token, CancellationToken ct)
    {
        if (
            !IsGithubConnected
            || _githubConnection is null
            || string.IsNullOrEmpty(token)
            || !string.IsNullOrWhiteSpace(NetworkSecret)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || !uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo)
            || GitHubProvider.FromRemote(url) is not { } target
        )
        {
            return true;
        }

        var version = Interlocked.Read(ref _repositorySelectionVersion);
        var connection = Interlocked.Read(ref _githubConnectionRevision);
        GitHubPushAccess access;
        try
        {
            access = await GithubPushAccessCheck(target, token, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return true;
        } // An API outage should not block the Git transport.

        if (
            version != Interlocked.Read(ref _repositorySelectionVersion)
            || connection != Interlocked.Read(ref _githubConnectionRevision)
            || _disposed
        )
        {
            return false;
        }

        if (access is GitHubPushAccess.Allowed or GitHubPushAccess.Unknown)
        {
            return true;
        }

        var repository = target.Owner + "/" + target.Name;
        var message = access switch
        {
            GitHubPushAccess.PrivateScopeNeeded => "GitLlama cannot access "
                + repository
                + ". Your authorization does not include private repositories. Enable private repository access and sign in again. Also check that the remote URL is correct.",
            GitHubPushAccess.NoWritePermission => "Your linked GitHub account does not have write access to "
                + repository
                + ". Ask the repository owner for write access; authorizing GitLlama cannot grant account permissions.",
            GitHubPushAccess.SignInRequired =>
                "GitHub rejected your saved sign-in. Reconnect GitHub in Settings > Integrations before pushing.",
            _ => "GitHub denied GitLlama access to "
                + repository
                + ". Review GitLlama's organization approval or SSO access, and check the remote URL. An organization owner may need to approve access.",
        };
        NeedsGithubPrivateAccess = access == GitHubPushAccess.PrivateScopeNeeded;
        if (access is GitHubPushAccess.ApprovalNeeded or GitHubPushAccess.PrivateScopeNeeded)
        {
            GithubPushAccessUrl =
                "https://github.com/settings/connections/applications/"
                + Uri.EscapeDataString(_githubConnection?.ClientId ?? GithubClientId);
        }

        Status = RemoteOperationFeedback = "Push failed: " + message;
        return false;
    }

    [RelayCommand]
    private async Task ManageGithubPushAccessAsync()
    {
        // Never open an arbitrary URL copied from a Git error or an API response.
        if (
            !Uri.TryCreate(GithubPushAccessUrl, UriKind.Absolute, out var uri)
            || !Views.IntegrationBrowserPolicy.Allows(uri)
        )
        {
            return;
        }

        try
        {
            if (OpenIntegrationBrowser is not null && await OpenIntegrationBrowser(uri))
            {
                return;
            }
        }
        catch { }

        Status = RemoteOperationFeedback =
            "The browser could not be opened. Open GitHub Settings > Applications > Authorized OAuth Apps > GitLlama to review access: "
            + uri.AbsoluteUri;
    }

    [RelayCommand]
    private async Task EnableGithubPrivateAccessAsync()
    {
        if (IsAuthenticationBusy || IsNetworkBusy)
        {
            return;
        }

        GithubIncludePrivateRepositories = true;
        IsGithubIntegrationExpanded = true;
        SelectedSettingsSection = "Integrations";
        IsSettingsOpen = true;
        IsGithubConnected = false;
        await SignInGithubOAuthCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ReconnectGithubPermissionsAsync()
    {
        if (IsAuthenticationBusy || IsNetworkBusy)
        {
            return;
        }

        IsGithubIntegrationExpanded = true;
        SelectedSettingsSection = "Integrations";
        IsSettingsOpen = true;
        IsGithubConnected = false;
        await SignInGithubOAuthCommand.ExecuteAsync(null);
        if (IsGithubConnected)
        {
            NeedsGithubWorkflowAccess = false;
            Status = RemoteOperationFeedback = "GitHub reconnected. Retry the push to apply your workflow changes.";
        }
    }
}
