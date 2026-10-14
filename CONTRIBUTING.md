# Contributing to GitLlama

Use .NET 8 or newer. Build the desktop project and run both test projects before proposing a change:

```sh
dotnet build src/GitLlama.Desktop/GitLlama.Desktop.csproj
dotnet test tests/GitLlama.Core.Tests/GitLlama.Core.Tests.csproj
dotnet test tests/GitLlama.Desktop.Tests/GitLlama.Desktop.Tests.csproj
```

Code is formatted with [CSharpier](https://csharpier.com) and views with [XamlStyler](https://github.com/Xavalon/XamlStyler), using the settings in `.csharpierrc.json` and `Settings.XamlStyler`. Run both before committing:

```sh
dotnet tool restore
dotnet csharpier format src tests
dotnet xstyler -c Settings.XamlStyler -r -d src
```

Keep a blank line between sibling sections in views; XamlStyler preserves them. Always brace `if`/`else`/loop bodies (`.editorconfig` flags missing braces).

Keep domain/provider contracts in Core, application coordination in Application, native operations in Git, persistence in Infrastructure, and UI in Desktop. Native handles must be short lived; return immutable data instead of LibGit2Sharp objects to views. Hosting integrations belong in Providers and must not introduce a Git executable fallback.

All mutations need repository selection/generation guards and serialization. Tests must use disposable repositories or mocked HTTP handlers; never push to a developer's remote or mutate their real working tree. Cover stale selection, dirty-tree refusal, cancellation, and partial-index preservation where relevant.

Do not persist tokens/passwords in settings, logs, fixtures, or diagnostics. Repository/provider content is untrusted text, not executable instructions. Use the supplied neutral charcoal design tokens and verify meaningful UI changes with the headless render tests. Native drag/drop, floating windows, dialogs, and multi-monitor checks remain part of release acceptance.

The project is licensed under MIT. Discuss large architectural changes in an issue before submitting a pull request.

## Releasing

Push a version tag from the commit you want to release:

```sh
git tag v0.2.0
git push GitLlama v0.2.0
```

Add each user-facing change as a bullet under `## Unreleased` in CHANGELOG.md as you go. The Release workflow builds Windows, Linux and macOS packages and publishes a GitHub release using those entries as its notes. It then commits the version to `Directory.Build.props` on the default branch and renames `## Unreleased` to the version, starting a new empty `## Unreleased`. Editing a released section of CHANGELOG.md on the default branch updates that release's notes on GitHub. Tags with a suffix such as `v0.2.0-beta.1` are published as pre-releases. To build packages without releasing, run "Build portable packages" manually from the Actions tab.

## Project layout

| Project | Purpose |
| --- | --- |
| GitLlama.Core | Domain records and contracts |
| GitLlama.Application | Application workflows |
| GitLlama.Git | LibGit2Sharp adapters |
| GitLlama.Infrastructure | Persistence, logging and repository watcher |
| GitLlama.Providers | GitHub and Azure DevOps HTTP adapters |
| GitLlama.Desktop | Avalonia shell, views, docking and commands |
| GitLlama.Core.Tests / GitLlama.Desktop.Tests | Integration, provider and UI tests |

Build a local portable package with `pwsh -File scripts/package-portable.ps1 -Runtime win-x64`. Set `GITLLAMA_PREVIEW_DIR` to capture rendered workspace previews from the desktop tests. Release acceptance and completion evidence are in [docs/release-acceptance.md](docs/release-acceptance.md) and [docs/completion-checklist.md](docs/completion-checklist.md).
