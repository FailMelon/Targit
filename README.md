# GitLlama

GitLlama is a desktop Git client for Windows, Linux and macOS. It shows your repositories on the left, the full commit history in the middle, and details for whatever you select on the right.

## Download

Get the latest version from the [Releases page](https://github.com/GitLlamaTeam/GitLlama/releases/latest). Each download is a single portable app with everything it needs, so there's nothing else to install.

| System | File |
| --- | --- |
| Windows (most PCs) | `GitLlama-<version>-win-x64.zip` |
| Windows on ARM | `GitLlama-<version>-win-arm64.zip` |
| Linux | `GitLlama-<version>-linux-x64.tar.gz` or `linux-arm64` |
| macOS (Apple Silicon) | `GitLlama-<version>-osx-arm64.tar.gz` |
| macOS (Intel) | `GitLlama-<version>-osx-x64.tar.gz` |

Extract it anywhere and run `GitLlama` (`GitLlama.exe` on Windows). The app isn't code-signed yet, so Windows may show a SmartScreen prompt (choose **More info → Run anyway**) and macOS may need you to right-click the app and choose **Open** the first time.

Windows is the most tested platform. The Linux and macOS builds are newer, so please [report anything that doesn't work](https://github.com/GitLlamaTeam/GitLlama/issues).

## What you can do

- **Repositories:** open, clone or create repositories in tabs, with recent repositories and favorites.
- **History:** a colored commit graph with author pictures, filters by branch, author and date, and search by message, author, SHA or ref.
- **Inspect:** commit details, changed files, colored diffs, file contents, file history and blame.
- **Commit:** stage whole files, hunks or individual lines, then commit or amend with a separate summary and description.
- **Branches and more:** branches, tags, stashes, merges with conflict resolution, cherry-pick, revert, reset and worktrees.
- **Remotes:** fetch, pull, push, add or edit remotes, and create, rename or delete branches on the server.
- **GitHub:** browse, create, review, comment on and merge pull requests, and manage issues.
- **Workspace:** dockable, rearrangeable panels with saved layouts, and automatic refresh when files change.

## Getting started

Click **+** beside the repository tabs to open, clone or create a repository. Pick a commit to see its details, or click the **working changes** button in the toolbar to stage files and commit. Right-click branches, tags, remotes and commits for more actions.

| Shortcut | Action |
| --- | --- |
| **Ctrl+O** | Open a repository |
| **Ctrl+F** | Search history |
| **Ctrl+Q** | Command palette |
| **F5** | Refresh |
| **Ctrl+Enter** | Commit staged changes |

To commit you need staged changes, a message and an author name and email. GitLlama uses your Git configuration for the author when it's set.

## Signing in

Open **Settings** from the cog in the title bar.

- **GitHub:** in **Integrations**, start GitHub sign-in and approve GitLlama in your browser with the code it shows. Access to private repositories is optional. On Windows you stay signed in between sessions. See [GitHub setup](docs/github-oauth.md).
- **Azure DevOps:** in **Integrations**, enter your organization URL and a personal access token to browse and clone its repositories. Pull requests and Boards aren't supported yet. See [Azure DevOps setup](docs/azure-devops.md).
- **Other HTTPS hosts:** GitLlama uses your Git credential helper, such as Git Credential Manager. You can also enter a token for the current session only.
- **SSH:** in **SSH**, choose your private key. OpenSSH keys work through your SSH agent, and PuTTY `.ppk` keys are unlocked inside GitLlama for the session. Pageant and Plink are available as an option. GitLlama asks you to confirm each new server fingerprint.

**Profiles** let you keep separate commit identities and SSH settings, for example for work and personal projects.

## Privacy

Passwords, tokens and SSH passphrases are never written to GitLlama's settings or logs. On Windows, saved GitHub and Azure DevOps sign-ins are encrypted for your user account.

Author and remote pictures come from GitHub, Gravatar and your remote hosts. Gravatar receives a hash of each commit author's email address. Turn pictures off in **Settings → General → Show author and remote pictures**.

Settings are stored in your system's application data folder under `GitLlama`.

## Current limitations

- Commit signing, Git LFS and Git hooks aren't run by GitLlama.
- Full interactive rebase isn't available yet.
- GitHub Enterprise and Azure DevOps pull requests aren't supported yet.
- History search covers the commits loaded so far (200 at a time; use **Load more** for older ones).

## Building from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download):

```sh
dotnet run --project src/GitLlama.Desktop/GitLlama.Desktop.csproj
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for tests, formatting and releases.

## License

GitLlama is [MIT licensed](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md) and [security](SECURITY.md).
