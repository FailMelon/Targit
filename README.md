# GitLlama

Authentication settings open in a centered overlay. HTTPS uses a host-scoped session token or your configured system Git credential helper, such as Git Credential Manager; helper storage follows your Git configuration. SSH operations require system Git/OpenSSH and use the local agent or an OpenSSH key file with known_hosts verification. Connect/check access verifies read access, while push permissions are checked during push. GitHub account verification supports pull requests and issues separately from Git transport credentials. Live browser sign-in, organization SSO and SSH access must be verified against the intended account/server.

GitLlama is a Git desktop client built with C#, Avalonia and LibGit2Sharp. Its compact charcoal workspace follows the supplied design: repository navigation on the left, full-height commit history in the center, and a contextual inspector on the right. Git operations use LibGit2Sharp; SSH and Azure PAT HTTPS transports use system Git.

## Run the app

Extract the Windows x64 portable package and open `GitLlama.exe`. The portable release bundles .NET and its libraries into one executable; no SDK installation is needed. License notices and documentation remain alongside it. Native libraries are automatically extracted to the .NET cache at startup. The + beside the repository tabs opens a repository manager listing saved repositories with Browse, Clone and Initialize actions. Initialize preserves existing project files; clone requires a new or empty destination. Source builds require the .NET 8 SDK:

```sh
dotnet restore GitLlama.sln
dotnet run --project src/GitLlama.Desktop/GitLlama.Desktop.csproj
```

Windows startup has been verified locally. The source and CI support Windows, Linux and macOS; native acceptance on Linux/macOS and ARM64 remains a release gate. See [release acceptance](docs/release-acceptance.md).

## Included workflows

- Open, initialize and clone repositories; recent repositories and favorites.
- Colored commit graph, history paging, branch/author/date filters and message/author/SHA/ref search.
- Historical commit details, changed files, colored patches, file content, file history and blame.
- Whole-file, hunk and selected-line staging/unstaging, bulk staging, patch export and staged commits/amend.
- Branches, tags, stashes, remotes, tracking, merges, conflict-result editing and guarded history operations.
- Native worktree creation, opening, locking and removal with a preserved folder backup; reflog recovery to a new branch.
- Submodule inspection, initialization and update.
- HTTPS username/token credentials, network progress and cancellation.
- GitHub pull requests and issues: lists, details, comments, creation, reviews, guarded merge and issue close/reopen.
- Automatic repository refresh, command palette, named dock layouts, settings and redacted diagnostics.

Open a repository with **Ctrl+O**, search history with **Ctrl+F**, open the command palette with **Ctrl+Q**, refresh with **F5**, and commit staged changes with **Ctrl+Enter**. The commit action requires staged changes, a message and an author identity. Git configuration fills the identity when available.

Choose a commit or historical file to inspect it; choose **Changes** to compose a staged commit. The inspector changes with the selection and has no tabs. The center changes between history, selected-file diffs, highlighted file contents and other workflows without document tabs. Commit details and staging/commit controls stay in the inspector. Repository tabs across the top switch workspaces; sidebar categories collapse independently. Staged and unstaged files have separate collapsible groups and per-file staging checkboxes. A pinned commit composer provides separate summary and description fields. The theme uses Discord-inspired charcoal and gray surfaces with restrained selection accents. Background refresh preserves the current context and reuses the mounted viewer. Right-click sidebar branches and tags or history commits for contextual actions. Drag the empty area of the top bar to move the borderless window; double-click to maximize/restore. Diffs use compact source-code rows, line numbers, a separate +/− gutter, syntax coloring and red/green backgrounds. Raw patch metadata is hidden while original line indexes remain intact for partial staging. Partial staging changes the index and preserves working-file content. Stash saving currently includes tracked changes. Worktree removal preserves files in a sibling backup directory before pruning registration. Cancelled clones can leave a partial destination for inspection.

## Credentials and hosting

Open Settings with the title-bar cogwheel or **File → Settings**. Configure accounts in **Settings → Authentication / Integrations**, and SSH in **Settings → SSH**. Browse for an OpenSSH private key and a public `.pub` file; a neighboring public key is detected automatically. Public keys can be previewed and copied, with their SHA256 fingerprint shown. Private key contents are never previewed or copied. Disable agent mode to authenticate with the selected private key; PuTTY `.ppk` keys require conversion to OpenSSH format. Manually entered tokens stay in memory for the current session and are excluded from saved settings and redacted from diagnostics; helper-managed credentials use the helper's storage. GitHub hosting supports `github.com` remotes; GitHub Enterprise API integration is not implemented. Use a dedicated test repository and permissions appropriate to the actions you intend to perform.

SSH is routed through system Git/OpenSSH and requires acceptance against the actual server, agent/key and known_hosts. Signing and Git LFS transfer are not release claims. Native library capabilities are shown in diagnostics. Full interactive rebase, arbitrary plugins, IDE features and AI features are outside the first-release scope.

GitHub browser sign-in is available in **Settings → Integrations**. Approve GitLlama using the displayed verification code; private-repository permissions are optional. GitHub authorization renews automatically. On Windows, OAuth tokens are encrypted per profile and restored after restart or profile switching. Disconnect removes the saved connection; revoked or expired refresh authorization requires sign-in again. See [GitHub integration](docs/github-oauth.md).

Azure DevOps Services is available in **Settings → Integrations** with organization-scoped personal access tokens, encrypted per-profile Windows storage, repository browsing/clone and HTTPS Git access. Enter the organization URL and its PAT; GitLlama verifies access before saving. Microsoft's newer Entra OAuth route does not currently support native personal-account access to Azure DevOps. Azure pull requests and Boards are not connected yet. See [Azure DevOps setup](docs/azure-devops.md).

## Workspace and settings

The sidebar and Remote screen group fetched branches beneath each named remote, with collapsible folders for branch prefixes. Right-click a remote to fetch it, open its settings, remove its configuration with confirmation, or copy its URL. Click branches to reveal their history; double-click to create and check out a local tracking branch. Collapsed folders remain collapsed during refresh. Right-click a remote to push the current local branch explicitly to that remote. The first push can publish to an empty remote and sets upstream only if none exists. Push feedback stays visible until dismissed, including rejected updates and authentication errors.

Use **Settings → Profiles** or the current-profile card to create, rename, switch or remove profiles. Each profile saves its commit name/email, SSH key paths, agent mode and credential-helper preference. A complete profile identity supplies commit authorship in GitLlama without rewriting repository Git configuration; blank identity fields fall back to the repository identity. Existing preferences migrate into a Default profile. Repository tabs, layout and refresh preferences remain shared. Switching clears manually entered session tokens and restores the selected profile's saved OAuth connection; the system credential helper and SSH agent remain shared across profiles.

Drag tabs to dock, split or float panes. Use **Window** to save/restore named layouts and select Review or Merge Resolution presets. Use **Window → Dock all documents**, or the History right-click docking action, to return floating documents. **View → History** also returns or recreates History. Layouts from the previous UI are backed up as `.previous` and migrated to the new arrangement.

Settings, recent repositories and the workspace are stored in the operating-system application-data directory under `GitLlama`. The status bar and diagnostics retain operation feedback without an Output pane. History loads pages of 200 commits; search applies to the loaded pages. Filtered rows omit connecting edges to avoid implying ancestry between unrelated visible commits.

## Development and verification

```sh
dotnet test tests/GitLlama.Core.Tests/GitLlama.Core.Tests.csproj -c Release
dotnet test tests/GitLlama.Desktop.Tests/GitLlama.Desktop.Tests.csproj -c Release
pwsh -File scripts/package-portable.ps1 -Runtime win-x64
```

Local verification passed 178 Git/service tests and 33 Avalonia desktop tests. These cover real disposable repositories, selective staging/commit/amend, recovery/worktrees, ignored and untracked files, watcher refresh, provider HTTP contracts, UI commands and rendering. The + opens an empty repository tab with the full-workspace manager. Opening, cloning or initializing fills that tab; clone/init collect details in dialogs. Open tabs and selection are restored on restart, and closing a tab preserves its recent-repository entry. Set `GITLLAMA_PREVIEW_DIR` to capture rendered workspace previews. Live authenticated GitHub/network actions and native cross-platform docking/dialog checks require separate acceptance.

CI builds/tests on Windows, Linux and macOS. The manually dispatched package workflow produces six architecture artifacts. Publishing includes application and dependency licenses. Release optimization and single-file packaging are separate settings; the package script enables both. Use a fresh output folder when changing packaging formats.

| Project | Purpose |
| --- | --- |
| GitLlama.Core | Domain records and contracts |
| GitLlama.Application | Application workflows |
| GitLlama.Git | LibGit2Sharp adapters |
| GitLlama.Infrastructure | Persistence, logging and repository watcher |
| GitLlama.Providers | GitHub HTTP adapter |
| GitLlama.Desktop | Avalonia shell, views, docking and commands |
| GitLlama.Core.Tests / GitLlama.Desktop.Tests | Integration, provider and UI verification |

See [completion evidence](docs/completion-checklist.md), [contributing](CONTRIBUTING.md), [security](SECURITY.md) and [third-party notices](THIRD_PARTY_NOTICES.md).
