# Phase 1 acceptance checklist

Phase 1 establishes the desktop shell, persistence, repository navigation and initial Git adapter. Staging, committing, remote operations and commit-graph rendering are future phases.

## Automated checks

```sh
dotnet build src/Targit.Desktop/Targit.Desktop.csproj -c Release
dotnet test tests/Targit.Core.Tests/Targit.Core.Tests.csproj -c Release
```

The GitHub Actions workflow runs these checks on Windows, Linux and macOS.

## Manual smoke test (required before merging)

Perform on Windows, macOS and Linux:

- [ ] Launch Targit and confirm charcoal backgrounds and readable text.
- [ ] Use **Open folder...** to select an existing Git repository.
- [ ] Confirm current branch, recent commits, changes and branches load.
- [ ] Open an empty repository, a repository with commits, and a bare repository without crashes.
- [ ] Open/close History, Changes, and Branches document tabs; verify selection and content remain in sync.
- [ ] Switch between two repositories; confirm content refreshes.
- [ ] Favorite a repository and restart; confirm it remains saved.
- [ ] Drag a tool tab to another dock and back.
- [ ] Float a tool panel into a new window and redock it.
- [ ] Resize dock panes; restart; confirm workspace restores.
- [ ] Save a named layout, change the arrangement, load it and confirm restoration.
- [ ] Reset to the default layout.
- [ ] Close and reopen Targit; verify recent repositories remain.
- [ ] Select a non-repository folder and confirm a visible, nonfatal error.
- [ ] Corrupt the workspace JSON and verify the application opens with a default layout.
- [ ] Switch repositories quickly during a refresh and verify old repository data does not overwrite the new selection.
- [ ] Inspect the app-data `Targit/targit.log` file for recorded errors.

## Scope notes

The phase provides functional docked document views but not a custom rendered Git DAG or staging/commit editor. The full Git feature set is planned for later phases. Cross-platform compilation is necessary but does not replace manual interaction tests.
