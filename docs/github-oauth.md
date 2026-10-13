# GitHub integration

Open Settings → Integrations → Sign in with GitHub. GitLlama requests a device code, opens https://github.com/login/device in your default browser, and shows the verification code. Enter it on GitHub, approve the displayed GitLlama app, and return to GitLlama. Copy code/Open GitHub sign-in page are available if automatic browser opening fails. Cancel stops polling.

Public repositories are requested by default (`public_repo read:user`). Select Include private repositories before signing in to request `repo read:user`. GitHub and organization policy control the actual access granted; SSO may require additional approval.

The connected token is used for pull requests/issues and GitHub HTTPS operations when no separate HTTPS session token is supplied. It is never sent to another host or used for SSH. On Windows, OAuth access/refresh tokens are encrypted with DPAPI for the current Windows user in a separate per-profile connection file. Tokens are excluded from ordinary settings and diagnostics. Restarting the app and switching profiles restore the corresponding saved connection. Disconnect removes the profile's local encrypted connection; revoke the app's authorization on GitHub if you want to revoke server-side access too.

Access tokens renew automatically before expiry, using the refresh token and persisting the rotated pair. An expired access token is renewed on startup. Temporary network failures trigger automatic retries; expired access tokens are excluded from Git HTTPS credentials. If GitHub rejects the refresh token as expired/revoked, GitLlama removes the saved connection and asks for sign-in again. The connected screen shows account status and Disconnect instead of a sign-in button or an access-token expiry warning. Older builds discarded refresh tokens, so sign in once in this build to enable renewal.

Secure persistence currently supports Windows. On other platforms, connections remain in memory and renew during the current app session, with the storage limitation shown in Integrations. Manually entered tokens remain session-only.

The public OAuth Client ID is configured in `GitHubDeviceOAuth.DefaultClientId`. Development builds may override it with `GITLLAMA_GITHUB_CLIENT_ID`. Device Flow must be enabled in the OAuth app registration; keep Expire user access tokens enabled. Client secrets are not required or bundled. The configured callback URL is unused by this flow.

Protocol reference: [GitHub OAuth authorization documentation](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow).

GitLab, Bitbucket and Azure DevOps integrations are planned and are not functional connections yet. Existing Git transports can still access repositories using their HTTPS/SSH credentials.
