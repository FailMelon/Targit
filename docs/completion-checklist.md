# Client completion checklist

This tracks the implementation against the supplied blueprint. Optional IDE/plugin/AI features and unverified native signing/LFS transports remain explicitly outside the first release.

- [x] Neutral charcoal shell, docking, contextual commit inspector and graph rendering
- [x] Clone and initialize repositories
- [x] History paging, branch/date/author filtering and commit context actions
- [x] Colored diffs, file/hunk/line staging and staged-content amend
- [x] File browser, blame, reflog and recovery
- [x] Native worktree create/open/lock/remove workflows
- [x] HTTPS credentials, network progress and cancellation
- [x] GitHub pull request and issue workflows with offline/error handling
- [x] Repository watching, keyboard command palette and layout presets
- [x] Settings, diagnostics, release packaging and contributor documentation
- [x] Integration tests and rendered UI verification for the completed workflows

Native cross-platform acceptance, authenticated external-service acceptance, code signing and notarization require their respective execution environments and credentials; local tests must not be presented as those release gates.

## Local evidence

- Release configuration: 178 Git/service integration and provider tests passed; 33 desktop tests passed, with no skipped tests.
- Azure integration verified using isolated HTTP responses: PAT verification/cancellation, encrypted per-profile token restore/removal, failed replacement preservation, repository browsing, URL normalization, denial errors and organization-scoped Git Basic authentication. Live repository access requires the user's organization-scoped PAT. Azure OAuth and MSAL have been removed.
- Push verified against disposable empty remotes, preserved upstream when publishing to a second remote, non-fast-forward rejection without force, server rejection callback propagation, missing-remote errors and persistent visible success/failure feedback.
- Automatic GitHub renewal verified with simulated short access-token lifetimes, token rotation, encrypted Windows DPAPI storage, expired-token renewal on restart, profile switching, offline retry, rejected/revoked refresh cleanup, disconnect removal and connected/sign-in visibility.
- GitHub device OAuth verified using isolated HTTP responses: pending/slow-down polling, approved/denied/expired codes, cancellation, fixed GitHub endpoints, scope selection, account verification, token expiry and GitHub-only HTTPS token scoping. Live browser approval remains a user-account acceptance check.
- Remote tree verified for multiple remotes, nested branch prefixes, counts, filtering, context menus, selected-remote/upstream targeting, unchanged node/expansion retention and rendered sidebar/Remote screen views.
- Profiles verified for legacy preference migration, isolated connection preferences, real commit authorship without rewriting Git configuration, duplicate-name validation, busy-operation guards, token clearing, restart restoration, removal and rendered settings layout.
- Settings cogwheel/File entry and public-key detection, preview, fingerprint, persisted paths and private-material rejection verified in desktop checks and a rendered SSH settings preview.
- Repository manager tab fills the workspace and hides navigation/inspector panes; switching back preserves the review, layout and commit draft.
- Rendered the main workspace at normal and compact sizes, empty repository state, changes, and all thirteen workflow documents.
- Windows native portable app startup verified against a real repository. An ignored-build-folder issue found during native inspection was corrected and covered by an integration regression test.
- Self-contained Windows x64 portable package includes license texts and release documentation.
- Provider actions exercised against isolated HTTP responses; destructive Git actions exercised in disposable repositories.

- Follow-up refinement: borderless draggable title bar, no Output pane, contextual inspector without tabs, branch/tag menus, and tested floating-document recovery.
- Working changes consolidated into the inspector; obsolete Changes documents removed from saved layouts.
- Contextual center file/diff viewer with line numbers and syntax highlighting, repository tabs, separate collapsible navigation categories and visible pane boundaries. Repository tab closure and index/working content are covered by regression checks.
- Modern visual refinement: blue accents, file-type badges, collapsible staged/unstaged groups with real checkbox staging, and a pinned summary/description commit composer.
- Context flicker regression: repeated refresh must emit no context transitions and retain the same center controls for history, repository files and working-file review. Charcoal/gray palette replaces the blue slate theme.
- Repository + opens a searchable manager with browse/open, clone and initialize; initialization preserves existing project files. Diff rendering uses contiguous code rows and a separate sign gutter with syntax highlighting; native patch indexes remain unchanged for staging.
