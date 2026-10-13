# Release acceptance

The portable package workflow builds six RID artifacts and runs the test suite on each operating-system runner. Cross-publishing a RID proves compilation and packaging, not native execution on that architecture.

Before publishing a release, record the outcomes of:

1. Local open/init/clone, staged/unstaged diffs, selective stage/commit/amend, branch/tag/stash workflows, and recovery.
2. Merge/conflict result editing, abort/continue, guarded reset/rebase, dirty worktree refusal, and removal backup verification.
3. History paging, filters, large-history scrolling, file content/history/blame, and submodules.
4. Authenticated HTTPS fetch/pull/push and GitHub private/public PR/issues operations with dedicated test credentials; confirm cancellation and offline behavior.
5. Native launch, keyboard/focus/accessibility, docking, floating windows, dialogs, layout restoration, multi-monitor/DPI, and automatic refresh on Windows, Linux, and macOS.
6. Native transport capabilities and RID-specific libgit2 assets. Validate system Git/OpenSSH transport against actual server credentials and known_hosts; verify helper browser sign-in, cancellation and SSO on the target host.
7. Portable artifact launch without a preinstalled .NET runtime, required native system dependencies, license notices, and platform installer/signing/notarization if used.

Do not report a local headless test run as passing the native or authenticated release gates. Never use a production repository for destructive acceptance tests.
