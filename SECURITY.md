# Security policy

Do not post tokens, private keys, repository credentials, or private repository content in issues or diagnostic attachments. Redact logs and reproduce problems in a disposable repository.

Report security vulnerabilities through the repository's private vulnerability reporting feature when enabled. If it is unavailable, ask maintainers for a private reporting channel without publishing exploit details or sensitive data.

GitLlama stores repository preferences and layouts locally. Manually entered HTTPS/GitHub tokens remain in memory. On Windows, OAuth access and refresh tokens are encrypted with DPAPI for the current Windows user in separate per-profile connection files; they are never written to ordinary settings or diagnostics. GitLlama rotates refresh tokens automatically, and Disconnect deletes the active profile's saved connection. Other platforms currently keep OAuth connections in memory. Token permissions should be limited to the operations the user needs. Native credentials and transport capabilities vary with the bundled libgit2 build.

Signing, Git LFS transfer, and arbitrary repository hooks are not executed. Worktree removal preserves a backup folder. Destructive resets, stash dropping, tag deletion, and PR merging require explicit actions and their applicable confirmations; those safeguards do not constitute an undo guarantee.
