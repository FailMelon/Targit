# Phase 2 — local changes and commits

Phase 2 builds on PR #1 and adds working-tree actions through LibGit2Sharp only.

## Implemented in PR #2

- Stage a selected changed file in the Changes dock.
- Unstage a selected file.
- Enter a commit message and author identity, then commit staged changes.
- Refresh the history and change list after each operation.
- Integration tests against temporary real repositories.

## Acceptance checklist

- [ ] Windows, macOS and Linux CI pass.
- [ ] Stage a new file and confirm it appears as staged.
- [ ] Unstage the file and confirm it returns to the working tree.
- [ ] Stage and commit a change; confirm new commit in History.
- [ ] Verify a blank commit message or author is rejected without a crash.
- [ ] Verify a failed Git operation is logged and the UI remains usable.
- [ ] Verify switching repositories does not commit changes in the wrong repository.
- [ ] Verify behavior on repositories with no HEAD commit.
- [ ] Verify the UI manually on Windows, macOS and Linux.

## Not yet included

Interactive hunks, diff viewer, amend, signing, remote push/pull, rebasing and merge conflict resolution are separate enhancements. Phase 2 is not complete until the acceptance checklist is verified.
