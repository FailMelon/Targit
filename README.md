# Targit

Targit is an open-source Git desktop client for **Windows, macOS and Linux** built with **C#**, **Avalonia**, and **LibGit2Sharp**. Git operations use LibGit2Sharp exclusively; there is no Git CLI fallback.

## Getting started

Install the .NET 8 SDK and run:

```sh
dotnet restore src/Targit.Desktop/Targit.Desktop.csproj
dotnet run --project src/Targit.Desktop/Targit.Desktop.csproj
```

To run tests:

```sh
dotnet test tests/Targit.Core.Tests/Targit.Core.Tests.csproj
```

GitHub Actions builds and tests on Windows, Linux and macOS.

## Desktop foundation

- Visual Studio-inspired neutral charcoal theme.
- Dock-powered workspace with draggable tabs, split panes and floating tool windows.
- Repository picker using native OS folder selection, recent repositories and favorites.
- History, working tree changes, branches, repository navigation, inspector and output panes.
- Repository inspection via LibGit2Sharp.
- JSON-persisted repository list and settings, plus serialized dock layout restoration.
- Core / Application / Git / Infrastructure / Desktop architecture.

The UI is a foundation for later Git operations. It does **not** yet implement staging, commits, rebasing, remotes, or a rendered multi-lane commit graph.

## Solution organization

| Project | Purpose |
| --- | --- |
| Targit.Core | Immutable domain records and Git contracts |
| Targit.Application | Application workflows |
| Targit.Git | LibGit2Sharp adapters |
| Targit.Infrastructure | JSON settings persistence |
| Targit.Desktop | Avalonia shell, docking and view models |
| Targit.Core.Tests | Unit and integration tests |

## UI and persistence

Open a repository using **Open folder...** or paste its path. Switch repositories using the top selector or the Repositories dock. Drag tool and document tabs to rearrange or float them. Dock layouts are stored under the OS application-data directory as `Targit/workspace.json`; recent repositories and tab settings are stored in `Targit/settings.json`. If a saved layout is incompatible, Targit falls back to the default layout.

## Contributing

Contributions are welcome. Please open an issue to discuss significant changes, keep platform-specific behavior behind abstractions, and add tests for Git adapters and persistence. All Git operations must use LibGit2Sharp; do not spawn `git`.

## Status

The initial Phase 1 desktop shell is under development in PR #1. CI passing is necessary but does not replace manual verification of docking, floating windows and workspace restoration on all three desktop platforms.
